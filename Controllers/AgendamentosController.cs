using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("agendamentos")]
    [Authorize(Roles = "Empresa,Funcionario")]
    public class AgendamentosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFinanceiroService _financeiroService;

        public AgendamentosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFinanceiroService financeiroService)
        {
            _context = context;
            _userManager = userManager;
            _financeiroService = financeiroService;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        // Não nulo só quando quem está logado é um Funcionário (não a Empresa
        // dona) — usado pra restringir a que só veja/mexa nos agendamentos
        // dele, sem afetar os colegas.
        private async Task<int?> GetFuncionarioIdAsync()
        {
            if (!User.IsInRole("Funcionario"))
                return null;

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

            return await _context.Funcionarios
                .Where(f => f.UserId == user.Id)
                .Select(f => (int?)f.Id)
                .FirstOrDefaultAsync();
        }

        // =========================
        // CARREGAR COMBOS
        // =========================
        private async Task CarregarCombos(int empresaId)
        {
            ViewBag.Clientes = await _context.Clientes
                .Where(c => c.Ativo &&
                    c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                .OrderBy(c => c.Nome)
                .ToListAsync() ?? new List<Cliente>();

            ViewBag.Servicos = await _context.Servicos
                .Where(s => s.Ativo && s.EmpresaId == empresaId)
                .OrderBy(s => s.Nome)
                .ToListAsync() ?? new List<Servico>();

            var funcionarioId = await GetFuncionarioIdAsync();

            var funcionariosQuery = _context.Funcionarios
                .Where(f => f.Ativo && f.EmpresaId == empresaId);

            // Funcionário só pode agendar em nome dele mesmo, nunca de um colega.
            if (funcionarioId.HasValue)
            {
                funcionariosQuery = funcionariosQuery.Where(f => f.Id == funcionarioId.Value);
            }

            ViewBag.Funcionarios = await funcionariosQuery
                .OrderBy(f => f.Nome)
                .ToListAsync() ?? new List<Funcionario>();
        }

        // =========================
        // INDEX
        // =========================
        [HttpGet("")]
        public async Task<IActionResult> Index( string? cliente, StatusAgendamento? status,  DateTime? dataInicial, DateTime? dataFinal,  int page = 1)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                int pageSize = 10;

                var funcionarioId = await GetFuncionarioIdAsync();

                var query = _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.Funcionario)
                    .Where(a => a.EmpresaId == empresaId);

                if (funcionarioId.HasValue)
                {
                    query = query.Where(a => a.FuncionarioId == funcionarioId.Value);
                }

                // CLIENTE
                if (!string.IsNullOrWhiteSpace(cliente))
                {
                    query = query.Where(a =>
                        (a.Cliente != null &&
                         a.Cliente.Nome.Contains(cliente))

                        ||

                        (a.NomeClienteAvulso != null &&
                         a.NomeClienteAvulso.Contains(cliente)));
                }

                // STATUS
                if (status.HasValue)
                {
                    query = query.Where(a =>
                        a.Status == status.Value);
                }

                // DATA INICIAL
                if (dataInicial.HasValue)
                {
                    query = query.Where(a =>
                        a.DataHora >= dataInicial.Value);
                }

                // DATA FINAL
                if (dataFinal.HasValue)
                {
                    query = query.Where(a =>
                        a.DataHora <= dataFinal.Value.AddDays(1));
                }

                query = query.OrderByDescending(a => a.DataHora);

                var totalItems = await query.CountAsync();

                var lista = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.Cliente = cliente;
                ViewBag.Status = status;
                ViewBag.DataInicial = dataInicial;
                ViewBag.DataFinal = dataFinal;

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages =
                    (int)Math.Ceiling(totalItems / (double)pageSize);

                return View(lista);
            }
            catch
            {
                ToastHelper.Error(
                    TempData,
                    "Erro ao carregar agendamentos.");

                return View(new List<Agendamento>());
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarStatus(
       int id,
       StatusAgendamento status)
        {
            var empresaId = await GetEmpresaId();
            var funcionarioId = await GetFuncionarioIdAsync();

            var agendamento = await _context.Agendamentos
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.EmpresaId == empresaId &&
                    (funcionarioId == null || x.FuncionarioId == funcionarioId));

            if (agendamento == null)
            {
                ToastHelper.Error(
                    TempData,
                    "Agendamento não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            agendamento.Status = status;
            agendamento.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Integração com o financeiro. A conta a receber já existe desde a
            // criação do agendamento; ao finalizar só apuramos a comissão (e,
            // como segurança, geramos a conta aqui também, caso este seja um
            // agendamento antigo criado antes dessa integração existir). Ao
            // cancelar, cancela/estorna a receita vinculada. Uma falha aqui não
            // deve impedir a troca de status já salva.
            try
            {
                if (status == StatusAgendamento.Finalizado)
                {
                    await _financeiroService.GerarContaReceberDeAgendamentoAsync(agendamento.Id);
                    await _financeiroService.ApurarComissaoDoAgendamentoAsync(agendamento.Id);
                }
                else if (status == StatusAgendamento.Cancelado)
                {
                    await _financeiroService.CancelarContaReceberDeAgendamentoAsync(
                        agendamento.Id,
                        "Agendamento cancelado.");
                }
            }
            catch
            {
                ToastHelper.Warning(
                    TempData,
                    "Status atualizado, mas houve um problema ao atualizar o financeiro.");

                return RedirectToAction(nameof(Index));
            }

            if (status == StatusAgendamento.Finalizado)
            {
                await ConsumirCreditoDoPlanoSeAplicavelAsync(agendamento);
            }

            return RedirectToAction(nameof(Index));
        }

        // Se o cliente tiver um plano ativo (ver módulo Planos) que cobre o
        // serviço desse agendamento, desconta 1 crédito do período atual —
        // sem isso, um plano nunca teria seu uso registrado. Não gera/altera
        // nada no financeiro (o cliente já "pagou" o plano por fora).
        private async Task ConsumirCreditoDoPlanoSeAplicavelAsync(Agendamento agendamento)
        {
            if (agendamento.ClienteId == null)
                return;

            try
            {
                var assinatura = await _context.AssinaturasPlanoServico
                    .Include(a => a.PlanoServico)
                    .Where(a =>
                        a.Status == Models.Enums.StatusAssinaturaPlano.Ativa &&
                        a.ClienteId == agendamento.ClienteId &&
                        a.PlanoServico.EmpresaId == agendamento.EmpresaId &&
                        a.PlanoServico.Servicos.Any(x => x.ServicoId == agendamento.ServicoId))
                    .FirstOrDefaultAsync();

                if (assinatura == null)
                    return;

                if (!assinatura.TemCreditoDisponivel())
                    return;

                assinatura.CreditosUsados++;
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Não bloqueia a finalização do agendamento por isso.
            }
        }

        // =========================
        // CREATE GET
        // =========================
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                await CarregarCombos(empresaId.Value);

                return View(new Agendamento());
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao abrir formulário.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(  Agendamento model,  bool funcionarioAleatorio)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                ModelState.Remove(nameof(Agendamento.EmpresaId));
                ModelState.Remove(nameof(Agendamento.Status));
                ModelState.Remove(nameof(Agendamento.Cliente));
                ModelState.Remove(nameof(Agendamento.Servico));
                ModelState.Remove(nameof(Agendamento.Empresa));
                ModelState.Remove(nameof(Agendamento.Funcionario));

                // =========================
                // CLIENTE AVULSO
                // =========================

                if (model.ClienteAvulso)
                {
                    model.ClienteId = null;

                    if (string.IsNullOrWhiteSpace(model.NomeClienteAvulso))
                    {
                        ModelState.AddModelError(
                            nameof(model.NomeClienteAvulso),
                            "Informe o nome do cliente.");
                    }

                    if (string.IsNullOrWhiteSpace(model.TelefoneClienteAvulso))
                    {
                        ModelState.AddModelError(
                            nameof(model.TelefoneClienteAvulso),
                            "Informe o telefone.");
                    }
                }
                else
                {
                    if (!model.ClienteId.HasValue)
                    {
                        ModelState.AddModelError(
                            nameof(model.ClienteId),
                            "Selecione um cliente.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    await CarregarCombos(empresaId.Value);
                    return View(model);
                }

                // =========================
                // SERVIÇO
                // =========================

                var servico = await _context.Servicos
                    .FirstOrDefaultAsync(s =>
                        s.Id == model.ServicoId &&
                        s.EmpresaId == empresaId);

                if (servico == null)
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");

                    await CarregarCombos(empresaId.Value);
                    return View(model);
                }

                // =========================
                // FUNCIONÁRIO
                // =========================

                var funcionarioLogadoId = await GetFuncionarioIdAsync();

                if (funcionarioLogadoId.HasValue)
                {
                    // Funcionário só agenda em nome dele mesmo — ignora
                    // qualquer valor de FuncionarioId vindo do formulário.
                    model.FuncionarioId = funcionarioLogadoId.Value;
                }
                else if (funcionarioAleatorio || !model.FuncionarioId.HasValue)
                {
                    var funcionarioDisponivel =
                        await _context.Funcionarios
                        .Where(f =>
                            f.EmpresaId == empresaId &&
                            f.Ativo)
                        .OrderBy(x => Guid.NewGuid())
                        .FirstOrDefaultAsync();

                    if (funcionarioDisponivel == null)
                    {
                        ToastHelper.Error(
                            TempData,
                            "Nenhum funcionário disponível.");

                        await CarregarCombos(empresaId.Value);
                        return View(model);
                    }

                    model.FuncionarioId = funcionarioDisponivel.Id;
                }

                // =========================
                // CONFLITO DE HORÁRIO
                // =========================

                var inicio = model.DataHora;

                var fim = inicio.AddMinutes(
                    servico.DuracaoMinutos);

                var conflito = await _context.Agendamentos
                    .Include(a => a.Servico)
                    .AnyAsync(a =>
                        a.EmpresaId == empresaId &&
                        a.FuncionarioId == model.FuncionarioId &&
                        a.Ativo &&
                        a.Status != StatusAgendamento.Cancelado &&
                        inicio < a.DataHora.AddMinutes(a.Servico.DuracaoMinutos) &&
                        fim > a.DataHora);

                if (conflito)
                {
                    ToastHelper.Warning(
                        TempData,
                        "Funcionário já possui agendamento nesse horário.");

                    await CarregarCombos(empresaId.Value);
                    return View(model);
                }

                // =========================
                // LIMITE DO PLANO (agendamentos/mês)
                // =========================

                var limiteAgendamentosMes = await _context.Empresas
                    .Where(e => e.Id == empresaId)
                    .Select(e => e.Plano != null ? e.Plano.LimiteAgendamentosMes : 0)
                    .FirstOrDefaultAsync();

                if (limiteAgendamentosMes > 0)
                {
                    var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    var fimMes = inicioMes.AddMonths(1);

                    var totalNoMes = await _context.Agendamentos.CountAsync(a =>
                        a.EmpresaId == empresaId &&
                        a.Ativo &&
                        a.DataCriacao >= inicioMes &&
                        a.DataCriacao < fimMes);

                    if (totalNoMes >= limiteAgendamentosMes)
                    {
                        ToastHelper.Warning(
                            TempData,
                            $"Seu plano permite até {limiteAgendamentosMes} agendamento(s) por mês. Faça upgrade do plano para continuar.");

                        await CarregarCombos(empresaId.Value);
                        return View(model);
                    }
                }

                // =========================
                // SALVAR
                // =========================

                model.EmpresaId = empresaId.Value;
                model.Status = StatusAgendamento.Agendado;
                model.Ativo = true;
                model.DataCriacao = DateTime.Now;

                _context.Agendamentos.Add(model);

                await _context.SaveChangesAsync();

                // Todo agendamento já entra no financeiro como previsão de
                // receita (Contas a Receber pendente) — não só quando finalizado.
                try
                {
                    await _financeiroService.GerarContaReceberDeAgendamentoAsync(model.Id);
                }
                catch
                {
                    ToastHelper.Warning(
                        TempData,
                        "Agendamento criado, mas houve um problema ao gerar a previsão no financeiro.");

                    return RedirectToAction(nameof(Index));
                }

                ToastHelper.Success(
                    TempData,
                    "Agendamento criado com sucesso.");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ToastHelper.Error(
                    TempData,
                    $"Erro ao criar agendamento: {ex.Message}");

                await CarregarCombos((await GetEmpresaId()) ?? 0);

                return View(model);
            }
        }
        // =========================
        // EDIT GET
        // =========================
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                var funcionarioId = await GetFuncionarioIdAsync();

                var agendamento = await _context.Agendamentos
                    .FirstOrDefaultAsync(a =>
                        a.Id == id &&
                        a.EmpresaId == empresaId &&
                        (funcionarioId == null || a.FuncionarioId == funcionarioId));

                if (agendamento == null)
                {
                    ToastHelper.Error(TempData, "Agendamento não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                await CarregarCombos(empresaId.Value);

                return View(agendamento);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar edição.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // EDIT POST
        // =========================
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]    
        public async Task<IActionResult> Edit( int id, Agendamento model, bool clienteAvulso, bool funcionarioAleatorio)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var funcionarioId = await GetFuncionarioIdAsync();

                var agendamento = await _context.Agendamentos
                    .FirstOrDefaultAsync(a =>
                        a.Id == id &&
                        a.EmpresaId == empresaId &&
                        (funcionarioId == null || a.FuncionarioId == funcionarioId));

                if (agendamento == null)
                {
                    ToastHelper.Error(TempData, "Agendamento não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                // Funcionário não pode passar o agendamento pra outro colega.
                if (funcionarioId.HasValue)
                {
                    funcionarioAleatorio = false;
                    model.FuncionarioId = funcionarioId.Value;
                }

                // =========================
                // CLIENTE AVULSO
                // =========================

                if (clienteAvulso)
                {
                    if (string.IsNullOrWhiteSpace(model.NomeClienteAvulso))
                    {
                        ToastHelper.Warning(
                            TempData,
                            "Informe o nome do cliente."
                        );

                        await CarregarCombos(empresaId.Value);
                        return View(model);
                    }

                    agendamento.ClienteAvulso = true;
                    agendamento.ClienteId = null;

                    agendamento.NomeClienteAvulso =
                        model.NomeClienteAvulso;

                    agendamento.TelefoneClienteAvulso =
                        model.TelefoneClienteAvulso;
                }
                else
                {
                    if (model.ClienteId == null)
                    {
                        ToastHelper.Warning(
                            TempData,
                            "Selecione um cliente."
                        );

                        await CarregarCombos(empresaId.Value);
                        return View(model);
                    }

                    agendamento.ClienteAvulso = false;
                    agendamento.ClienteId = model.ClienteId;

                    agendamento.NomeClienteAvulso = null;
                    agendamento.TelefoneClienteAvulso = null;
                }

                // =========================
                // SERVIÇO
                // =========================

                var servico = await _context.Servicos
                    .FirstOrDefaultAsync(s =>
                        s.Id == model.ServicoId &&
                        s.EmpresaId == empresaId);

                if (servico == null)
                {
                    ToastHelper.Error(
                        TempData,
                        "Serviço não encontrado."
                    );

                    await CarregarCombos(empresaId.Value);
                    return View(model);
                }

                // =========================
                // FUNCIONÁRIO
                // =========================

                if (funcionarioAleatorio)
                {
                    var funcionarios = await _context.Funcionarios
                        .Where(f =>
                            f.EmpresaId == empresaId &&
                            f.Ativo)
                        .ToListAsync();

                    var funcionarioAleatorioId = funcionarios
                        .OrderBy(x => Guid.NewGuid())
                        .Select(x => (int?)x.Id)
                        .FirstOrDefault();

                    if (!funcionarioAleatorioId.HasValue)
                    {
                        ToastHelper.Error(
                            TempData,
                            "Nenhum funcionário disponível."
                        );

                        await CarregarCombos(empresaId.Value);
                        return View(model);
                    }

                    agendamento.FuncionarioId =
                        funcionarioAleatorioId.Value;
                }
                else
                {
                    agendamento.FuncionarioId =
                        model.FuncionarioId;
                }

                // =========================
                // VALIDA CONFLITO HORÁRIO
                // =========================

                var inicio = model.DataHora;
                var fim = inicio.AddMinutes(servico.DuracaoMinutos);

                var conflito = await _context.Agendamentos
                    .Include(a => a.Servico)
                    .AnyAsync(a =>
                        a.Id != agendamento.Id &&
                        a.EmpresaId == empresaId &&
                        a.FuncionarioId == agendamento.FuncionarioId &&
                        a.Ativo &&
                        a.Status != StatusAgendamento.Cancelado &&
                        inicio < a.DataHora.AddMinutes(a.Servico.DuracaoMinutos) &&
                        fim > a.DataHora);

                if (conflito)
                {
                    ToastHelper.Warning(
                        TempData,
                        "Já existe um agendamento para este funcionário nesse horário."
                    );

                    await CarregarCombos(empresaId.Value);
                    return View(model);
                }

                // =========================
                // DADOS PRINCIPAIS
                // =========================

                agendamento.ServicoId = model.ServicoId;
                agendamento.DataHora = model.DataHora;
                agendamento.Observacao = model.Observacao;
                agendamento.DataAtualizacao = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                ToastHelper.Success(
                    TempData,
                    "Agendamento atualizado com sucesso."
                );

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ToastHelper.Error(
                    TempData,
                    $"Erro ao atualizar agendamento: {ex.Message}"
                );

                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // DELETE (INATIVAR)
        // =========================
        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                var funcionarioId = await GetFuncionarioIdAsync();

                var agendamento = await _context.Agendamentos
                    .FirstOrDefaultAsync(a =>
                        a.Id == id &&
                        a.EmpresaId == empresaId &&
                        (funcionarioId == null || a.FuncionarioId == funcionarioId));

                if (agendamento == null)
                {
                    ToastHelper.Error(TempData, "Agendamento não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                agendamento.Ativo = false;

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Agendamento removido.");
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao remover.");
                return RedirectToAction(nameof(Index));
            }
        }

        // ✅ /agendamentos/calendario
        [HttpGet("calendario")]
        public IActionResult Calendario()
        {
            return View();
        }

        // ✅ /agendamentos/eventos
        [HttpGet("eventos")]
        public async Task<IActionResult> GetEventos()
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                    return Json(new List<object>());

                var funcionarioId = await GetFuncionarioIdAsync();

                var eventosQuery = _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.Funcionario)
                    .Where(a =>
                        a.EmpresaId == empresaId &&
                        a.Ativo);

                if (funcionarioId.HasValue)
                {
                    eventosQuery = eventosQuery.Where(a => a.FuncionarioId == funcionarioId.Value);
                }

                var eventos = await eventosQuery.ToListAsync();

                var lista = eventos.Select(a => new
                {
                    id = a.Id,

                    title =
                        (a.Cliente?.Nome ??
                         a.NomeClienteAvulso ??
                         "Cliente")
                        + " - " +
                        (a.Servico?.Nome ?? ""),

                    start = a.DataHora,

                    end = a.DataHora.AddMinutes(
                        a.Servico?.DuracaoMinutos ?? 30),

                    backgroundColor = a.Status switch
                    {
                        StatusAgendamento.Agendado => "#0d6efd",
                        StatusAgendamento.Confirmado => "#198754",
                        StatusAgendamento.Cancelado => "#dc3545",
                        StatusAgendamento.Finalizado => "#6c757d",
                        _ => "#0d6efd"
                    },

                    borderColor = a.Status switch
                    {
                        StatusAgendamento.Agendado => "#0d6efd",
                        StatusAgendamento.Confirmado => "#198754",
                        StatusAgendamento.Cancelado => "#dc3545",
                        StatusAgendamento.Finalizado => "#6c757d",
                        _ => "#0d6efd"
                    },

                    extendedProps = new
                    {
                        funcionario = a.Funcionario?.Nome,
                        servico = a.Servico?.Nome,
                        status = a.Status.ToString()
                    }
                });

                return Json(lista);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    erro = true,
                    mensagem = ex.Message
                });
            }
        }
    }
}