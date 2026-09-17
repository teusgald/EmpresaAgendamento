using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using EmpresaAgendamento.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("")]
    public class PublicoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IFinanceiroService _financeiroService;
        private readonly INotificacaoAgendamentoService _notificacaoAgendamentoService;
        private readonly INotificacaoService _notificacaoService;
        private readonly IPlanoCreditoService _planoCreditoService;

        public PublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IFinanceiroService financeiroService,
            INotificacaoAgendamentoService notificacaoAgendamentoService,
            INotificacaoService notificacaoService,
            IPlanoCreditoService planoCreditoService)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _financeiroService = financeiroService;
            _notificacaoAgendamentoService = notificacaoAgendamentoService;
            _notificacaoService = notificacaoService;
            _planoCreditoService = planoCreditoService;
        }

        // Link público do recibo (mandado por e-mail pro cliente quando o
        // pagamento é confirmado) — token opaco, sem exigir login.
        [HttpGet("recibo/{token:guid}")]
        public async Task<IActionResult> Recibo(Guid token)
        {
            var conta = await _context.ContasReceber
                .Include(c => c.Cliente)
                .Include(c => c.Agendamento).ThenInclude(a => a!.Servico)
                .Include(c => c.Empresa)
                .FirstOrDefaultAsync(c => c.ReciboToken == token);

            if (conta == null || conta.ValorRecebido <= 0)
                return NotFound();

            return View("~/Views/ContasReceber/Recibo.cshtml", conta);
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Index(string slug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return NotFound();

                slug = slug.Trim().ToLower();

                var empresa = await _context.Empresas
                    .AsNoTracking()
                    .Include(e => e.Servicos)
                    .Include(e => e.Fotos)
                    .Include(e => e.Horarios)
                    .Include(e => e.Avaliacoes).ThenInclude(a => a.Cliente)
                    .Include(e => e.PlanosServico).ThenInclude(p => p.Servicos).ThenInclude(x => x.Servico)
                    .FirstOrDefaultAsync(e =>
                        e.Slug.ToLower() == slug &&
                        e.Ativo);

                if (empresa == null)
                    return NotFound();

                var avaliacoes = empresa.Avaliacoes
                    .OrderByDescending(a => a.DataAtualizacao ?? a.DataCriacao)
                    .ToList();

                var viewModel = new EmpresaPublicaViewModel
                {
                    Empresa = empresa,
                    Servicos = empresa.Servicos
                        .OrderBy(x => x.Nome)
                        .ToList(),
                    Fotos = empresa.Fotos
                        .OrderBy(f => f.DataUpload)
                        .ToList(),
                    PlanosServico = empresa.PlanosServico
                        .Where(p => p.Ativo)
                        .OrderBy(p => p.Nome)
                        .ToList(),
                    Avaliacoes = avaliacoes,
                    TotalAvaliacoes = avaliacoes.Count,
                    NotaMedia = avaliacoes.Count > 0
                        ? avaliacoes.Average(a => a.Nota)
                        : null
                };

                return View(viewModel);
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpGet("Publico/Funcionarios/{empresaId}")]
        public async Task<IActionResult> Funcionarios(int empresaId)
        {
            var funcionarios = await _context.Funcionarios
                .Where(x =>
                    x.EmpresaId == empresaId &&
                    x.Ativo)
                .Select(x => new
                {
                    x.Id,
                    x.Nome
                })
                .ToListAsync();

            return Json(funcionarios);
        }

        [HttpGet("Publico/HorariosDisponiveis")]
        public async Task<IActionResult> HorariosDisponiveis(
            int empresaId,
            int? servicoId,
            int? funcionarioId,
            DateTime data,
            int? excluirAgendamentoId = null)
        {
            // Duração do serviço define o tamanho real do slot — sem isso,
            // caímos de volta pra um bloco de 30 min só por compatibilidade.
            var duracaoMinutos = 30;

            if (servicoId.HasValue)
            {
                var servico = await _context.Servicos
                    .FirstOrDefaultAsync(s =>
                        s.Id == servicoId.Value &&
                        s.EmpresaId == empresaId &&
                        s.Ativo);

                if (servico == null)
                    return Json(new List<string>());

                duracaoMinutos = servico.DuracaoMinutos;
            }

            var diaSemana = data.DayOfWeek;

            var horarioEmpresa = await _context.EmpresasHorarios
                .FirstOrDefaultAsync(h => h.EmpresaId == empresaId && h.DiaSemana == diaSemana);

            var agendamentosDia = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a =>
                    a.EmpresaId == empresaId &&
                    a.Ativo &&
                    a.Status != StatusAgendamento.Cancelado &&
                    a.DataHora.Date == data.Date &&
                    (excluirAgendamentoId == null || a.Id != excluirAgendamentoId))
                .ToListAsync();

            var funcionariosQuery = _context.Funcionarios
                .Include(f => f.Horarios)
                .Where(f => f.EmpresaId == empresaId && f.Ativo);

            if (funcionarioId.HasValue)
            {
                funcionariosQuery = funcionariosQuery.Where(f => f.Id == funcionarioId.Value);
            }

            var funcionarios = await funcionariosQuery.ToListAsync();

            var slotsDisponiveis = new SortedSet<string>();

            if (funcionarios.Count == 0)
            {
                // Empresa sem funcionário cadastrado (ou nenhum bate o filtro
                // pedido) — agenda direto na empresa, sem vincular a um
                // profissional específico.
                var expedienteEmpresa = ResolverExpediente(horarioEmpresa, diaSemana);

                if (expedienteEmpresa != null)
                {
                    var agendamentosSemFuncionario = agendamentosDia
                        .Where(a => a.FuncionarioId == null)
                        .ToList();

                    AdicionarSlots(slotsDisponiveis, expedienteEmpresa.Value, data, duracaoMinutos, agendamentosSemFuncionario);
                }

                return Json(slotsDisponiveis.ToList());
            }

            foreach (var funcionario in funcionarios)
            {
                var horarioDia = funcionario.Horarios.FirstOrDefault(h => h.DiaSemana == diaSemana);

                // Funcionário já com expediente próprio configurado pra esse
                // dia — respeita à risca (inclusive dia de folga). Sem
                // configuração própria, cai pro horário de funcionamento da
                // empresa (ou o padrão antigo, se a empresa também não tiver).
                var expediente = horarioDia != null
                    ? (horarioDia.TrabalhaNoDia
                        ? new Expediente(horarioDia.HoraInicio, horarioDia.HoraFim, horarioDia.InicioIntervalo, horarioDia.FimIntervalo)
                        : (Expediente?)null)
                    : ResolverExpediente(horarioEmpresa, diaSemana);

                if (expediente == null)
                    continue;

                var agendamentosFuncionario = agendamentosDia
                    .Where(a => a.FuncionarioId == funcionario.Id)
                    .ToList();

                AdicionarSlots(slotsDisponiveis, expediente.Value, data, duracaoMinutos, agendamentosFuncionario);
            }

            return Json(slotsDisponiveis.ToList());
        }

        private readonly record struct Expediente(
            TimeSpan Inicio,
            TimeSpan Fim,
            TimeSpan? InicioIntervalo,
            TimeSpan? FimIntervalo);

        private static Expediente? ResolverExpediente(EmpresaHorario? horarioEmpresa, DayOfWeek diaSemana)
        {
            if (horarioEmpresa != null)
            {
                return horarioEmpresa.TrabalhaNoDia
                    ? new Expediente(horarioEmpresa.HoraInicio, horarioEmpresa.HoraFim, horarioEmpresa.InicioIntervalo, horarioEmpresa.FimIntervalo)
                    : null;
            }

            // Empresa nunca configurou horário de funcionamento: janela
            // padrão (compatibilidade), segunda a sábado, 08h-18h.
            if (diaSemana == DayOfWeek.Sunday)
                return null;

            return new Expediente(new TimeSpan(8, 0, 0), new TimeSpan(18, 0, 0), null, null);
        }

        private static void AdicionarSlots(
            SortedSet<string> slotsDisponiveis,
            Expediente expediente,
            DateTime data,
            int duracaoMinutos,
            List<Agendamento> agendamentosExistentes)
        {
            for (var horaSlot = expediente.Inicio;
                 horaSlot + TimeSpan.FromMinutes(duracaoMinutos) <= expediente.Fim;
                 horaSlot += TimeSpan.FromMinutes(30))
            {
                var inicioSlot = data.Date + horaSlot;
                var fimSlot = inicioSlot.AddMinutes(duracaoMinutos);

                if (inicioSlot < DateTime.Now)
                    continue;

                if (expediente.InicioIntervalo.HasValue && expediente.FimIntervalo.HasValue)
                {
                    var inicioPausa = data.Date + expediente.InicioIntervalo.Value;
                    var fimPausa = data.Date + expediente.FimIntervalo.Value;

                    if (inicioSlot < fimPausa && fimSlot > inicioPausa)
                        continue;
                }

                var conflito = agendamentosExistentes.Any(a =>
                    inicioSlot < a.DataHora.AddMinutes(a.Servico.DuracaoMinutos) &&
                    fimSlot > a.DataHora);

                if (!conflito)
                {
                    slotsDisponiveis.Add(horaSlot.ToString(@"hh\:mm"));
                }
            }
        }
        [HttpGet("login")]
        public IActionResult Login(int? empresaId)
        {
            var model = new ClienteLoginViewModel
            {
                EmpresaId = empresaId
            };

            return View(model);
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(ClienteLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new
                {
                    success = false,
                    error = "Dados inválidos."
                });

            var users = await _userManager.Users
                .Where(u => u.Email == model.Email)
                .ToListAsync();

            ApplicationUser? user = null;

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Cliente"))
                {
                    user = u;
                    break;
                }
            }

            if (user == null)
            {
                // Mesma mensagem de senha errada — não dá pra revelar se o
                // e-mail tem conta de cliente só pelo texto do erro de login.
                return Json(new
                {
                    success = false,
                    error = "Email ou senha inválidos."
                });
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                false,
                true);

            if (!result.Succeeded)
            {
                return Json(new
                {
                    success = false,
                    error = result.IsLockedOut
                        ? "Muitas tentativas de login. Tente novamente em alguns minutos."
                        : result.IsNotAllowed
                            ? "Confirme seu e-mail antes de entrar. Verifique sua caixa de entrada."
                            : "Email ou senha inválidos."
                });
            }

            // LOGIN PELO SITE PÚBLICO
            if (model.EmpresaId.HasValue)
            {
                return Json(new
                {
                    success = true,
                    redirect = $"/Cliente/Agendamentos?empresaId={model.EmpresaId}"
                });
            }

            // LOGIN NORMAL
            return Json(new
            {
                success = true,
                redirect = "/Cliente"
            });
        }

        [HttpPost("Publico/Agendar")]
        public async Task<IActionResult> Agendar( [FromBody] AgendamentoPublicoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Dados inválidos."
                });
            }

            // ======================================
            // SERVIÇO (precisa pertencer à empresa informada)
            // ======================================
            var servico = await _context.Servicos
                .FirstOrDefaultAsync(s =>
                    s.Id == model.ServicoId &&
                    s.EmpresaId == model.EmpresaId &&
                    s.Ativo);

            if (servico == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Serviço não encontrado."
                });
            }

            // ======================================
            // QUALQUER PROFISSIONAL (empresa sem funcionário cadastrado
            // agenda direto nela mesma — FuncionarioId fica nulo)
            // ======================================
            if (!model.FuncionarioId.HasValue)
            {
                model.FuncionarioId = await _context.Funcionarios
                    .Where(f =>
                        f.EmpresaId == model.EmpresaId &&
                        f.Ativo)
                    .OrderBy(x => Guid.NewGuid())
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync();
            }
            else
            {
                // Funcionário informado precisa pertencer à mesma empresa
                var funcionarioValido = await _context.Funcionarios
                    .AnyAsync(f =>
                        f.Id == model.FuncionarioId &&
                        f.EmpresaId == model.EmpresaId &&
                        f.Ativo);

                if (!funcionarioValido)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Profissional não encontrado."
                    });
                }
            }

            // ======================================
            // CONFLITO DE HORÁRIO (overlap real, considerando a duração do serviço)
            // ======================================
            var inicio = model.DataHora;
            var fim = inicio.AddMinutes(servico.DuracaoMinutos);

            var conflito = await _context.Agendamentos
                .Include(a => a.Servico)
                .AnyAsync(a =>
                    a.EmpresaId == model.EmpresaId &&
                    a.FuncionarioId == model.FuncionarioId &&
                    a.Ativo &&
                    a.Status != StatusAgendamento.Cancelado &&
                    inicio < a.DataHora.AddMinutes(a.Servico.DuracaoMinutos) &&
                    fim > a.DataHora);

            if (conflito)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este horário acabou de ser ocupado. Escolha outro horário."
                });
            }

            // ======================================
            // LIMITE DO PLANO (agendamentos/mês)
            // ======================================
            var limiteAgendamentosMes = await _context.Empresas
                .Where(e => e.Id == model.EmpresaId)
                .Select(e => e.Plano != null ? e.Plano.LimiteAgendamentosMes : 0)
                .FirstOrDefaultAsync();

            if (limiteAgendamentosMes > 0)
            {
                var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var fimMes = inicioMes.AddMonths(1);

                var totalNoMes = await _context.Agendamentos.CountAsync(a =>
                    a.EmpresaId == model.EmpresaId &&
                    a.Ativo &&
                    a.DataCriacao >= inicioMes &&
                    a.DataCriacao < fimMes);

                if (totalNoMes >= limiteAgendamentosMes)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Esta empresa atingiu o limite de agendamentos do mês. Entre em contato diretamente com o estabelecimento."
                    });
                }
            }

            // ======================================
            // CLIENTE LOGADO
            // ======================================
            int? clienteId = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);

                var user = await _userManager.Users
                    .FirstOrDefaultAsync(x =>
                        x.Id == userId &&
                        x.ClienteId.HasValue);

                if (user != null)
                {
                    clienteId = user.ClienteId;
                }
            }

            // Cliente que agenda direto pelo público passa a "pertencer" a
            // essa empresa (aparece na lista de Clientes dela) — antes, só
            // quem a empresa cadastrava manualmente ficava vinculado.
            if (clienteId.HasValue)
            {
                await EmpresaClienteHelper.GarantirVinculoAsync(_context, model.EmpresaId, clienteId.Value);
            }

            // ======================================
            // AGENDAMENTO
            // ======================================
            var agendamento = new Agendamento
            {
                EmpresaId = model.EmpresaId,
                ServicoId = model.ServicoId,
                FuncionarioId = model.FuncionarioId,
                DataHora = model.DataHora,

                Status = StatusAgendamento.Agendado,

                ClienteId = clienteId,

                ClienteAvulso = clienteId == null,

                NomeClienteAvulso = clienteId == null
                    ? model.Nome
                    : null,

                TelefoneClienteAvulso = clienteId == null
                    ? model.Telefone
                    : null,

                DataCriacao = DateTime.UtcNow,
                Ativo = true
            };

            _context.Agendamentos.Add(agendamento);

            await _context.SaveChangesAsync();

            // Se o cliente logado tem plano ativo que cobre esse serviço, já
            // usa 1 crédito do período agora (cliente avulso não tem como
            // ter plano).
            if (clienteId.HasValue)
            {
                var assinaturaUsada = await _planoCreditoService.ConsumirSeAplicavelAsync(
                    agendamento.EmpresaId, clienteId.Value, agendamento.ServicoId);

                if (assinaturaUsada.HasValue)
                {
                    agendamento.AssinaturaPlanoServicoId = assinaturaUsada;
                    await _context.SaveChangesAsync();
                }
            }

            // Todo agendamento já entra no financeiro como previsão de receita
            // (Contas a Receber pendente) — mesmo criado pelo cliente aqui.
            try
            {
                await _financeiroService.GerarContaReceberDeAgendamentoAsync(agendamento.Id);
            }
            catch
            {
                // Não bloqueia o agendamento do cliente por um problema no financeiro.
            }

            await _notificacaoAgendamentoService.EnviarConfirmacaoAsync(agendamento.Id);

            await _notificacaoService.NotificarEmpresaAsync(
                agendamento.EmpresaId,
                TipoNotificacao.Agendamento,
                "Novo agendamento",
                $"{model.Nome} agendou para {model.DataHora:dd/MM/yyyy HH:mm}.",
                "/Agendamentos");

            if (clienteId.HasValue)
            {
                await _notificacaoService.NotificarClienteAsync(
                    clienteId.Value,
                    agendamento.EmpresaId,
                    TipoNotificacao.Agendamento,
                    "Agendamento confirmado",
                    $"Seu agendamento para {model.DataHora:dd/MM/yyyy HH:mm} foi confirmado.",
                    "/Cliente/Agendamentos");
            }

            return Ok(new
            {
                sucesso = true,
                mensagem = "Agendamento realizado com sucesso."
            });
        }

        // ======================================
        // AVALIAÇÕES
        // ======================================

        // Avaliação existente do cliente logado pra essa empresa (se houver)
        // — usado pra pré-preencher o formulário quando ele já avaliou antes.
        [HttpGet("Publico/MinhaAvaliacao")]
        public async Task<IActionResult> MinhaAvaliacao(int empresaId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { avaliado = false });
            }

            var userId = _userManager.GetUserId(User);

            var clienteId = await _userManager.Users
                .Where(u => u.Id == userId && u.ClienteId.HasValue)
                .Select(u => u.ClienteId)
                .FirstOrDefaultAsync();

            if (clienteId == null)
            {
                return Json(new { avaliado = false });
            }

            var avaliacao = await _context.Avaliacoes
                .Where(a => a.EmpresaId == empresaId && a.ClienteId == clienteId)
                .Select(a => new { a.Nota, a.Comentario })
                .FirstOrDefaultAsync();

            if (avaliacao == null)
            {
                return Json(new { avaliado = false });
            }

            return Json(new
            {
                avaliado = true,
                nota = avaliacao.Nota,
                comentario = avaliacao.Comentario
            });
        }

        [HttpPost("Publico/Avaliar")]
        public async Task<IActionResult> Avaliar([FromBody] AvaliacaoViewModel model)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { sucesso = false, mensagem = "Você precisa estar logado para avaliar." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { sucesso = false, mensagem = "Escolha de 1 a 5 estrelas." });
            }

            var userId = _userManager.GetUserId(User);

            var clienteId = await _userManager.Users
                .Where(u => u.Id == userId && u.ClienteId.HasValue)
                .Select(u => u.ClienteId)
                .FirstOrDefaultAsync();

            if (clienteId == null)
            {
                return Json(new { sucesso = false, mensagem = "Você precisa estar logado como cliente para avaliar." });
            }

            var empresaExiste = await _context.Empresas
                .AnyAsync(e => e.Id == model.EmpresaId && e.Ativo);

            if (!empresaExiste)
            {
                return Json(new { sucesso = false, mensagem = "Empresa não encontrada." });
            }

            var avaliacao = await _context.Avaliacoes
                .FirstOrDefaultAsync(a => a.EmpresaId == model.EmpresaId && a.ClienteId == clienteId);

            if (avaliacao == null)
            {
                _context.Avaliacoes.Add(new Avaliacao
                {
                    EmpresaId = model.EmpresaId,
                    ClienteId = clienteId.Value,
                    Nota = model.Nota,
                    Comentario = model.Comentario
                });
            }
            else
            {
                avaliacao.Nota = model.Nota;
                avaliacao.Comentario = model.Comentario;
                avaliacao.DataAtualizacao = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { sucesso = true, mensagem = "Avaliação enviada. Obrigado!" });
        }

        // ======================================
        // PLANOS — CLIENTE SOLICITA (fica pendente até a empresa aprovar)
        // ======================================

        [HttpPost("Publico/SolicitarPlano")]
        public async Task<IActionResult> SolicitarPlano([FromBody] SolicitarPlanoViewModel model)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { sucesso = false, mensagem = "Você precisa estar logado para contratar um plano." });
            }

            var userId = _userManager.GetUserId(User);

            var clienteId = await _userManager.Users
                .Where(u => u.Id == userId && u.ClienteId.HasValue)
                .Select(u => u.ClienteId)
                .FirstOrDefaultAsync();

            if (clienteId == null)
            {
                return Json(new { sucesso = false, mensagem = "Você precisa estar logado como cliente pra contratar um plano." });
            }

            var plano = await _context.PlanosServico
                .FirstOrDefaultAsync(p => p.Id == model.PlanoServicoId && p.Ativo);

            if (plano == null)
            {
                return Json(new { sucesso = false, mensagem = "Plano não encontrado." });
            }

            var jaAssinante = await _context.AssinaturasPlanoServico
                .AnyAsync(a =>
                    a.PlanoServicoId == model.PlanoServicoId &&
                    a.ClienteId == clienteId &&
                    (a.Status == StatusAssinaturaPlano.Ativa || a.Status == StatusAssinaturaPlano.Pendente));

            if (jaAssinante)
            {
                return Json(new { sucesso = false, mensagem = "Você já solicitou ou já assina este plano." });
            }

            var assinatura = new AssinaturaPlanoServico
            {
                PlanoServicoId = model.PlanoServicoId,
                ClienteId = clienteId.Value,
                Status = StatusAssinaturaPlano.Pendente
            };

            _context.AssinaturasPlanoServico.Add(assinatura);

            await _context.SaveChangesAsync();

            var nomeCliente = await _context.Clientes
                .Where(c => c.Id == clienteId)
                .Select(c => c.Nome)
                .FirstOrDefaultAsync();

            await _notificacaoService.NotificarEmpresaAsync(
                plano.EmpresaId,
                TipoNotificacao.Plano,
                "Novo pedido de plano",
                $"{nomeCliente ?? "Um cliente"} solicitou o plano \"{plano.Nome}\".",
                $"/PlanosServico/Assinantes/{plano.Id}");

            return Json(new
            {
                sucesso = true,
                mensagem = "Solicitação enviada! A empresa vai confirmar o pagamento com você e ativar seu plano."
            });
        }
    }
}