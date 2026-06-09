using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("agendamentos")]
    [Authorize(Roles = "Empresa")]
    public class AgendamentosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AgendamentosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
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

            ViewBag.Funcionarios = await _context.Funcionarios
                .Where(f => f.Ativo && f.EmpresaId == empresaId)
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

                var query = _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.Funcionario)
                    .Where(a => a.EmpresaId == empresaId);

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

            var agendamento = await _context.Agendamentos
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.EmpresaId == empresaId);

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

            return RedirectToAction(nameof(Index));
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

                if (funcionarioAleatorio || !model.FuncionarioId.HasValue)
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
                // SALVAR
                // =========================

                model.EmpresaId = empresaId.Value;
                model.Status = StatusAgendamento.Agendado;
                model.Ativo = true;
                model.DataCriacao = DateTime.Now;

                _context.Agendamentos.Add(model);

                await _context.SaveChangesAsync();

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

                var agendamento = await _context.Agendamentos
                    .FirstOrDefaultAsync(a => a.Id == id && a.EmpresaId == empresaId);

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

                var agendamento = await _context.Agendamentos
                    .FirstOrDefaultAsync(a =>
                        a.Id == id &&
                        a.EmpresaId == empresaId);

                if (agendamento == null)
                {
                    ToastHelper.Error(TempData, "Agendamento não encontrado.");
                    return RedirectToAction(nameof(Index));
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

                var agendamento = await _context.Agendamentos
                    .FirstOrDefaultAsync(a => a.Id == id && a.EmpresaId == empresaId);

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

                var eventos = await _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.Funcionario)
                    .Where(a =>
                        a.EmpresaId == empresaId &&
                        a.Ativo)
                    .ToListAsync();

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