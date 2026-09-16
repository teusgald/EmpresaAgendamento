using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using EmpresaAgendamento.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public PublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IFinanceiroService financeiroService)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _financeiroService = financeiroService;
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
                    .FirstOrDefaultAsync(e =>
                        e.Slug.ToLower() == slug &&
                        e.Ativo);

                if (empresa == null)
                    return NotFound();

                var viewModel = new EmpresaPublicaViewModel
                {
                    Empresa = empresa,
                    Servicos = empresa.Servicos
                        .OrderBy(x => x.Nome)
                        .ToList()
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
            DateTime data)
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

            var funcionariosQuery = _context.Funcionarios
                .Include(f => f.Horarios)
                .Where(f => f.EmpresaId == empresaId && f.Ativo);

            if (funcionarioId.HasValue)
            {
                funcionariosQuery = funcionariosQuery.Where(f => f.Id == funcionarioId.Value);
            }

            var funcionarios = await funcionariosQuery.ToListAsync();

            var agendamentosDia = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a =>
                    a.EmpresaId == empresaId &&
                    a.Ativo &&
                    a.Status != StatusAgendamento.Cancelado &&
                    a.DataHora.Date == data.Date)
                .ToListAsync();

            var slotsDisponiveis = new SortedSet<string>();

            foreach (var funcionario in funcionarios)
            {
                TimeSpan inicioExpediente;
                TimeSpan fimExpediente;
                TimeSpan? inicioIntervalo = null;
                TimeSpan? fimIntervalo = null;

                if (funcionario.Horarios.Any())
                {
                    // Já configurou expediente real — respeita à risca,
                    // inclusive dia de folga.
                    var horarioDia = funcionario.Horarios
                        .FirstOrDefault(h => h.DiaSemana == diaSemana);

                    if (horarioDia == null || !horarioDia.TrabalhaNoDia)
                        continue;

                    inicioExpediente = horarioDia.HoraInicio;
                    fimExpediente = horarioDia.HoraFim;
                    inicioIntervalo = horarioDia.InicioIntervalo;
                    fimIntervalo = horarioDia.FimIntervalo;
                }
                else
                {
                    // Nunca configurou expediente: janela padrão (compatibilidade),
                    // segunda a sábado, 08h-18h.
                    if (diaSemana == DayOfWeek.Sunday)
                        continue;

                    inicioExpediente = new TimeSpan(8, 0, 0);
                    fimExpediente = new TimeSpan(18, 0, 0);
                }

                var agendamentosFuncionario = agendamentosDia
                    .Where(a => a.FuncionarioId == funcionario.Id)
                    .ToList();

                for (var horaSlot = inicioExpediente;
                     horaSlot + TimeSpan.FromMinutes(duracaoMinutos) <= fimExpediente;
                     horaSlot += TimeSpan.FromMinutes(30))
                {
                    var inicioSlot = data.Date + horaSlot;
                    var fimSlot = inicioSlot.AddMinutes(duracaoMinutos);

                    if (inicioSlot < DateTime.Now)
                        continue;

                    if (inicioIntervalo.HasValue && fimIntervalo.HasValue)
                    {
                        var inicioPausa = data.Date + inicioIntervalo.Value;
                        var fimPausa = data.Date + fimIntervalo.Value;

                        if (inicioSlot < fimPausa && fimSlot > inicioPausa)
                            continue;
                    }

                    var conflito = agendamentosFuncionario.Any(a =>
                        inicioSlot < a.DataHora.AddMinutes(a.Servico.DuracaoMinutos) &&
                        fimSlot > a.DataHora);

                    if (!conflito)
                    {
                        slotsDisponiveis.Add(horaSlot.ToString(@"hh\:mm"));
                    }
                }
            }

            return Json(slotsDisponiveis.ToList());
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
                return Json(new
                {
                    success = false,
                    error = "Usuário não encontrado."
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
                redirect = "/Cliente/Agendamentos"
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
            // QUALQUER PROFISSIONAL
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

                if (!model.FuncionarioId.HasValue)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Nenhum profissional disponível."
                    });
                }
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

            return Ok(new
            {
                sucesso = true,
                mensagem = "Agendamento realizado com sucesso."
            });
        }
    }
}