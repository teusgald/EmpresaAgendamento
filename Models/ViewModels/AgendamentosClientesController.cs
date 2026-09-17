using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Route("Cliente/Agendamentos")]
[Authorize(Roles = "Cliente")]
public class AgendamentosClientesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IFinanceiroService _financeiroService;
    private readonly INotificacaoAgendamentoService _notificacaoAgendamentoService;
    private readonly INotificacaoService _notificacaoService;
    private readonly IPlanoCreditoService _planoCreditoService;

    public AgendamentosClientesController(
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

    private async Task<ApplicationUser?> GetCurrentUserAsync()
        => await _userManager.GetUserAsync(User);

    private IActionResult RedirectLogin()
        => RedirectToAction("Login", "ClientesAuth");

    // =========================
    // LISTA
    // =========================
    [HttpGet("")]
    public async Task<IActionResult> Index(string? aba)
    {
        var user = await GetCurrentUserAsync();

        if (user == null || user.ClienteId == null)
            return RedirectLogin();

        aba = string.IsNullOrWhiteSpace(aba) ? "proximos" : aba.ToLowerInvariant();

        var query = _context.Agendamentos
            .AsNoTracking()
            .Include(a => a.Servico)
            .Include(a => a.Empresa)
            .Where(a => a.ClienteId == user.ClienteId);

        // Por padrão só mostra o que ainda vai acontecer — vencido/cancelado
        // vira aba de histórico separada, não fica tudo empilhado junto.
        query = aba switch
        {
            "historico" => query.Where(a =>
                a.Status == StatusAgendamento.Finalizado ||
                a.Status == StatusAgendamento.Cancelado),
            _ => query.Where(a =>
                a.Status == StatusAgendamento.Agendado ||
                a.Status == StatusAgendamento.Confirmado)
        };

        query = aba == "historico"
            ? query.OrderByDescending(a => a.DataHora)
            : query.OrderBy(a => a.DataHora);

        var agendamentos = await query.ToListAsync();

        ViewBag.Aba = aba;

        return View(agendamentos);
    }

    // =========================
    // FORM CRIAR (NORMAL)
    // =========================
    [HttpGet("novo")]
    public async Task<IActionResult> Create()
    {
        var user = await GetCurrentUserAsync();

        if (user == null || user.ClienteId == null)
            return RedirectLogin();

        ViewBag.Segmentos = await _context.Empresas
            .Where(e => e.Ativo && e.SegmentoAtuacao != null)
            .Select(e => e.SegmentoAtuacao!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        ViewBag.Empresas = new SelectList(
            await _context.Empresas.Where(e => e.Ativo).ToListAsync(),
            "Id", "Nome"
        );

        return View();
    }

    // =========================
    // AJAX EMPRESAS (filtradas por categoria/segmento)
    // =========================
    [HttpGet("empresas")]
    public async Task<IActionResult> Empresas(string? segmento)
    {
        var query = _context.Empresas.Where(e => e.Ativo);

        if (!string.IsNullOrWhiteSpace(segmento))
        {
            query = query.Where(e => e.SegmentoAtuacao == segmento);
        }

        var empresas = await query
            .OrderBy(e => e.Nome)
            .Select(e => new { e.Id, e.Nome })
            .ToListAsync();

        return Json(empresas);
    }

    // =========================
    // CRIAR (NORMAL)
    // =========================
    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Agendamento agendamento)
    {
        var user = await GetCurrentUserAsync();

        if (user == null || user.ClienteId == null)
            return RedirectLogin();

        ModelState.Remove(nameof(Agendamento.FuncionarioId));
        ModelState.Remove(nameof(Agendamento.Status));
        ModelState.Remove(nameof(Agendamento.Cliente));
        ModelState.Remove(nameof(Agendamento.Servico));
        ModelState.Remove(nameof(Agendamento.Empresa));
        ModelState.Remove(nameof(Agendamento.Funcionario));

        agendamento.ClienteId = user.ClienteId.Value;
        agendamento.ClienteAvulso = false;

        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = new SelectList(
                await _context.Empresas.Where(e => e.Ativo).ToListAsync(),
                "Id", "Nome");
            return View(agendamento);
        }

        // =========================
        // SERVIÇO (precisa pertencer à empresa selecionada)
        // =========================
        var servico = await _context.Servicos
            .FirstOrDefaultAsync(s =>
                s.Id == agendamento.ServicoId &&
                s.EmpresaId == agendamento.EmpresaId &&
                s.Ativo);

        if (servico == null)
        {
            ModelState.AddModelError(
                nameof(Agendamento.ServicoId),
                "Serviço não encontrado para a empresa selecionada.");

            ViewBag.Empresas = new SelectList(
                await _context.Empresas.Where(e => e.Ativo).ToListAsync(),
                "Id", "Nome");
            return View(agendamento);
        }

        // Cliente agendando por conta própria passa a "pertencer" a essa
        // empresa (aparece na lista de Clientes dela).
        await EmpresaClienteHelper.GarantirVinculoAsync(_context, agendamento.EmpresaId, user.ClienteId.Value);

        // =========================
        // FUNCIONÁRIO (o formulário não coleta esse campo — sorteia entre os
        // disponíveis; sem nenhum cadastrado, agenda direto na empresa)
        // =========================
        var funcionarioId = await _context.Funcionarios
            .Where(f => f.EmpresaId == agendamento.EmpresaId && f.Ativo)
            .OrderBy(x => Guid.NewGuid())
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        agendamento.FuncionarioId = funcionarioId;

        // =========================
        // CONFLITO DE HORÁRIO (overlap real, considerando a duração do serviço)
        // =========================
        var inicio = agendamento.DataHora;
        var fim = inicio.AddMinutes(servico.DuracaoMinutos);

        var conflito = await _context.Agendamentos
            .Include(a => a.Servico)
            .AnyAsync(a =>
                a.EmpresaId == agendamento.EmpresaId &&
                a.FuncionarioId == funcionarioId &&
                a.Ativo &&
                a.Status != StatusAgendamento.Cancelado &&
                inicio < a.DataHora.AddMinutes(a.Servico.DuracaoMinutos) &&
                fim > a.DataHora);

        if (conflito)
        {
            ModelState.AddModelError(
                "",
                "Não há profissional disponível nesse horário. Escolha outro horário.");

            ViewBag.Empresas = new SelectList(
                await _context.Empresas.Where(e => e.Ativo).ToListAsync(),
                "Id", "Nome");
            return View(agendamento);
        }

        // LIMITE DO PLANO (agendamentos/mês)
        var limiteAgendamentosMes = await _context.Empresas
            .Where(e => e.Id == agendamento.EmpresaId)
            .Select(e => e.Plano != null ? e.Plano.LimiteAgendamentosMes : 0)
            .FirstOrDefaultAsync();

        if (limiteAgendamentosMes > 0)
        {
            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var fimMes = inicioMes.AddMonths(1);

            var totalNoMes = await _context.Agendamentos.CountAsync(a =>
                a.EmpresaId == agendamento.EmpresaId &&
                a.Ativo &&
                a.DataCriacao >= inicioMes &&
                a.DataCriacao < fimMes);

            if (totalNoMes >= limiteAgendamentosMes)
            {
                ModelState.AddModelError(
                    "",
                    "Esta empresa atingiu o limite de agendamentos do mês. Entre em contato diretamente com o estabelecimento.");

                ViewBag.Empresas = new SelectList(
                    await _context.Empresas.Where(e => e.Ativo).ToListAsync(),
                    "Id", "Nome");
                return View(agendamento);
            }
        }

        agendamento.Status = StatusAgendamento.Agendado;
        agendamento.Ativo = true;
        agendamento.DataCriacao = DateTime.UtcNow;

        _context.Agendamentos.Add(agendamento);
        await _context.SaveChangesAsync();

        // Se o cliente tem plano ativo que cobre esse serviço, já usa 1
        // crédito do período agora — não só quando a empresa finalizar.
        var assinaturaUsada = await _planoCreditoService.ConsumirSeAplicavelAsync(
            agendamento.EmpresaId, agendamento.ClienteId!.Value, agendamento.ServicoId);

        if (assinaturaUsada.HasValue)
        {
            agendamento.AssinaturaPlanoServicoId = assinaturaUsada;
            await _context.SaveChangesAsync();
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

        var clienteNome = await _context.Clientes
            .Where(c => c.Id == agendamento.ClienteId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync();

        await _notificacaoService.NotificarEmpresaAsync(
            agendamento.EmpresaId,
            TipoNotificacao.Agendamento,
            "Novo agendamento",
            $"{clienteNome ?? "Um cliente"} agendou para {agendamento.DataHora:dd/MM/yyyy HH:mm}.",
            "/Agendamentos");

        await _notificacaoService.NotificarClienteAsync(
            agendamento.ClienteId!.Value,
            agendamento.EmpresaId,
            TipoNotificacao.Agendamento,
            "Agendamento confirmado",
            $"Seu agendamento para {agendamento.DataHora:dd/MM/yyyy HH:mm} foi confirmado.",
            "/Cliente/Agendamentos");

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // EDITAR
    // =========================
    [HttpGet("editar/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await GetCurrentUserAsync();

        if (user == null || user.ClienteId == null)
            return RedirectLogin();

        var agendamento = await _context.Agendamentos
            .FirstOrDefaultAsync(x => x.Id == id && x.ClienteId == user.ClienteId);

        if (agendamento == null)
            return NotFound();

        ViewBag.Empresas = await _context.Empresas.ToListAsync();

        return View(agendamento);
    }

    // =========================
    // DELETE
    // =========================
    [HttpPost("excluir/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await GetCurrentUserAsync();

        if (user == null || user.ClienteId == null)
            return RedirectLogin();

        var agendamento = await _context.Agendamentos
            .FirstOrDefaultAsync(x => x.Id == id && x.ClienteId == user.ClienteId);

        if (agendamento != null)
        {
            var assinaturaId = agendamento.AssinaturaPlanoServicoId;

            _context.Agendamentos.Remove(agendamento);
            await _context.SaveChangesAsync();

            if (assinaturaId.HasValue)
            {
                await _planoCreditoService.DevolverCreditoAsync(assinaturaId.Value);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // AJAX SERVIÇOS
    // =========================
    [HttpGet("servicos/{empresaId}")]
    public async Task<IActionResult> Servicos(int empresaId)
    {
        var servicos = await _context.Servicos
            .Where(x => x.EmpresaId == empresaId)
            .Select(x => new { x.Id, x.Nome })
            .ToListAsync();

        return Json(servicos);
    }

    // Agendamento público sem login é feito por PublicoController.Agendar
    // (esta rota duplicada foi removida: a view "PublicoAgendamento" nunca existiu
    // e o POST não validava tenant nem conflito de horário).
}