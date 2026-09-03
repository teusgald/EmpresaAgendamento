using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
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

    public AgendamentosClientesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
        => await _userManager.GetUserAsync(User);

    private IActionResult RedirectLogin()
        => RedirectToAction("Login", "ClientesAuth");

    // =========================
    // LISTA
    // =========================
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();

        if (user == null || user.ClienteId == null)
            return RedirectLogin();

        var agendamentos = await _context.Agendamentos
            .AsNoTracking()
            .Include(a => a.Servico)
            .Include(a => a.Empresa)
            .Where(a => a.ClienteId == user.ClienteId)
            .OrderByDescending(a => a.DataCriacao)
            .ToListAsync();

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

        ViewBag.Empresas = new SelectList(
            await _context.Empresas.Where(e => e.Ativo).ToListAsync(),
            "Id", "Nome"
        );

        return View();
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

        agendamento.ClienteId = user.ClienteId.Value;

        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _context.Empresas.ToListAsync();
            return View(agendamento);
        }

        _context.Agendamentos.Add(agendamento);
        await _context.SaveChangesAsync();

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
            _context.Agendamentos.Remove(agendamento);
            await _context.SaveChangesAsync();
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

    // =========================
    // 🔥 NOVO: AGENDAMENTO PÚBLICO (SEM LOGIN)
    // =========================
    [AllowAnonymous]
    [HttpGet("publico/{empresaId}")]
    public async Task<IActionResult> Publico(int empresaId)
    {
        var empresa = await _context.Empresas
            .FirstOrDefaultAsync(x => x.Id == empresaId && x.Ativo);

        if (empresa == null)
            return NotFound();

        ViewBag.Empresa = empresa;

        ViewBag.Servicos = await _context.Servicos
            .Where(x => x.EmpresaId == empresaId)
            .ToListAsync();

        return View("PublicoAgendamento");
    }

    // =========================
    // 🔥 CRIAR AGENDAMENTO PÚBLICO
    // =========================
    [AllowAnonymous]
    [HttpPost("publico")]
    public async Task<IActionResult> PublicoCreate(Agendamento agendamento)
    {
        if (!ModelState.IsValid)
            return RedirectToAction("Publico", new { empresaId = agendamento.EmpresaId });

        agendamento.ClienteId = null; // cliente opcional
        agendamento.DataCriacao = DateTime.UtcNow;

        _context.Agendamentos.Add(agendamento);
        await _context.SaveChangesAsync();

        return RedirectToAction("PublicoConfirmacao");
    }

    [AllowAnonymous]
    [HttpGet("publico-confirmacao")]
    public IActionResult PublicoConfirmacao()
    {
        return View();
    }
}