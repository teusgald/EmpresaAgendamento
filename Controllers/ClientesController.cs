using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Empresa,Funcionario")]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificacaoService _notificacaoService;
    private readonly ILogger<ClientesController> _logger;

    public ClientesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificacaoService notificacaoService,
        ILogger<ClientesController> logger)
    {
        _context = context;
        _userManager = userManager;
        _notificacaoService = notificacaoService;
        _logger = logger;
    }

    private async Task<int?> GetEmpresaId()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.EmpresaId;
    }

    // =========================
    // INDEX
    // =========================
    [HttpGet("/Clientes")]
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada. Faça login novamente.");
                return RedirectToAction("Login", "Account");
            }

            // Cobre agendamentos feitos antes do vínculo passar a ser criado
            // automaticamente — sem isso, cliente que só agendou (nunca foi
            // cadastrado manualmente) continuaria de fora da lista.
            await GarantirVinculosRetroativosAsync(empresaId.Value);

            int pageSize = 10;

            var query = _context.Clientes
                .Where(c => c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                .OrderBy(c => c.Nome);

            var totalItems = await query.CountAsync();

            var clientesDaPagina = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var idsDaPagina = clientesDaPagina.Select(c => c.Id).ToList();

            // Histórico de agendamentos (só os desta empresa) — total e
            // última visita finalizada. Duas consultas simples em vez de uma
            // só com dois agregados filtrados diferentes — combinar os dois
            // num único GroupBy já deu problema de tradução pro SQL antes.
            var totalAgendamentos = await _context.Agendamentos
                .Where(a => a.EmpresaId == empresaId && a.Ativo && a.ClienteId != null && idsDaPagina.Contains(a.ClienteId.Value))
                .GroupBy(a => a.ClienteId!.Value)
                .Select(g => new { ClienteId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.ClienteId, x => x.Total);

            var ultimasVisitas = await _context.Agendamentos
                .Where(a => a.EmpresaId == empresaId && a.Status == StatusAgendamento.Finalizado && a.ClienteId != null && idsDaPagina.Contains(a.ClienteId.Value))
                .GroupBy(a => a.ClienteId!.Value)
                .Select(g => new { ClienteId = g.Key, Ultima = g.Max(a => a.DataHora) })
                .ToDictionaryAsync(x => x.ClienteId, x => x.Ultima);

            // Plano ativo (dessa empresa, se houver).
            var planosAtivos = await _context.AssinaturasPlanoServico
                .Where(a =>
                    idsDaPagina.Contains(a.ClienteId) &&
                    a.Status == StatusAssinaturaPlano.Ativa &&
                    a.PlanoServico.EmpresaId == empresaId)
                .Select(a => new { a.ClienteId, a.PlanoServico.Nome })
                .ToDictionaryAsync(x => x.ClienteId, x => x.Nome);

            // Gasto total já recebido (contas a receber quitadas/parciais).
            var gastos = await _context.ContasReceber
                .Where(c => c.EmpresaId == empresaId && c.ClienteId != null && idsDaPagina.Contains(c.ClienteId.Value))
                .GroupBy(c => c.ClienteId!.Value)
                .Select(g => new { ClienteId = g.Key, Total = g.Sum(c => c.ValorRecebido) })
                .ToDictionaryAsync(x => x.ClienteId, x => x.Total);

            var lista = clientesDaPagina.Select(c => new ClienteListaItemViewModel
            {
                Id = c.Id,
                Nome = c.Nome,
                Email = c.Email,
                Telefone = c.Telefone,
                Ativo = c.Ativo,
                TotalAgendamentos = totalAgendamentos.TryGetValue(c.Id, out var total) ? total : 0,
                UltimaVisita = ultimasVisitas.TryGetValue(c.Id, out var ultima) ? ultima : null,
                PlanoAtivo = planosAtivos.TryGetValue(c.Id, out var plano) ? plano : null,
                GastoTotal = gastos.TryGetValue(c.Id, out var gasto) ? gasto : 0
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar clientes.");
            ToastHelper.Error(TempData, "Erro ao carregar clientes.");
            return RedirectToAction("Index", "Home");
        }
    }

    private async Task GarantirVinculosRetroativosAsync(int empresaId)
    {
        var clienteIdsComAgendamento = await _context.Agendamentos
            .Where(a => a.EmpresaId == empresaId && a.ClienteId != null)
            .Select(a => a.ClienteId!.Value)
            .Distinct()
            .ToListAsync();

        if (clienteIdsComAgendamento.Count == 0)
            return;

        var clienteIdsJaVinculados = await _context.EmpresaClientes
            .Where(ec => ec.EmpresaId == empresaId)
            .Select(ec => ec.ClienteId)
            .ToListAsync();

        var faltantes = clienteIdsComAgendamento.Except(clienteIdsJaVinculados).ToList();

        if (faltantes.Count == 0)
            return;

        foreach (var clienteId in faltantes)
        {
            _context.EmpresaClientes.Add(new EmpresaCliente { EmpresaId = empresaId, ClienteId = clienteId });
        }

        await _context.SaveChangesAsync();
    }

    // =========================
    // CREATE GET
    // =========================
    [HttpGet]
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

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir cadastro de cliente.");
            ToastHelper.Error(TempData, "Erro ao abrir cadastro.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // CREATE POST
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Cliente cliente)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                ToastHelper.Warning(TempData, "Preencha os campos obrigatórios.");
                return View(cliente);
            }

            cliente.Ativo = true;

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            _context.EmpresaClientes.Add(new EmpresaCliente
            {
                ClienteId = cliente.Id,
                EmpresaId = empresaId.Value
            });

            await _context.SaveChangesAsync();

            await _notificacaoService.NotificarEmpresaAsync(
                empresaId.Value,
                TipoNotificacao.Cadastro,
                "Novo cliente cadastrado",
                $"{cliente.Nome} foi cadastrado(a).",
                "/Clientes");

            ToastHelper.Success(TempData, "Cliente cadastrado com sucesso.");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar cliente.");
            ToastHelper.Error(TempData, "Erro ao criar cliente.");
            return View(cliente);
        }
    }

    // =========================
    // EDIT GET
    // =========================
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

            if (cliente == null)
            {
                ToastHelper.Error(TempData, "Cliente não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            return View(cliente);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir cliente {ClienteId}.", id);
            ToastHelper.Error(TempData, "Erro ao abrir cliente.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // EDIT POST
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Cliente cliente)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            if (id != cliente.Id)
            {
                ToastHelper.Error(TempData, "Cliente inválido.");
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                ToastHelper.Warning(TempData, "Verifique os dados informados.");
                return View(cliente);
            }

            var existente = await _context.Clientes
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

            if (existente == null)
            {
                ToastHelper.Error(TempData, "Cliente não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            existente.Nome = cliente.Nome;
            existente.Email = cliente.Email;
            existente.Telefone = cliente.Telefone;

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Cliente atualizado com sucesso.");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar cliente {ClienteId}.", id);
            ToastHelper.Error(TempData, "Erro ao atualizar cliente.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // TOGGLE ATIVO
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAtivo(int id)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

            if (cliente == null)
            {
                ToastHelper.Error(TempData, "Cliente não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            cliente.Ativo = !cliente.Ativo;

            await _context.SaveChangesAsync();

            if (cliente.Ativo)
                ToastHelper.Success(TempData, "Cliente ativado com sucesso.");
            else
                ToastHelper.Warning(TempData, "Cliente inativado com sucesso.");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar status do cliente {ClienteId}.", id);
            ToastHelper.Error(TempData, "Erro ao alterar status do cliente.");
            return RedirectToAction(nameof(Index));
        }
    }
}