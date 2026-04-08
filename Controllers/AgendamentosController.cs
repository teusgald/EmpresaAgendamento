using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("agendamentos")]
[Authorize(Roles = "Empresa")]
public class AgendamentosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AgendamentosController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<int?> GetEmpresaId()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.EmpresaId;
    }

    // ✅ /agendamentos
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var empresaId = await GetEmpresaId();
        if (empresaId == null) return RedirectToAction("Login", "Account");

        var lista = await _context.Agendamentos
            .Include(a => a.Cliente)
            .Include(a => a.Servico)
            .Where(a => a.EmpresaId == empresaId)
            .ToListAsync();

        return View(lista);
    }

    // ✅ /agendamentos/create
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var empresaId = await GetEmpresaId();
        if (empresaId == null) return RedirectToAction("Login", "Account");

        int id = empresaId.Value;

        ViewBag.Clientes = await _context.Clientes
            .Where(c => c.EmpresaClientes.Any(ec => ec.EmpresaId == id))
            .ToListAsync();

        ViewBag.Servicos = await _context.Servicos
            .Where(s => s.EmpresaId == id)
            .ToListAsync();

        return View();
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(Agendamento model)
    {
        var empresaId = await GetEmpresaId();
        if (empresaId == null) return RedirectToAction("Login", "Account");

        ModelState.Remove("EmpresaId");

        if (ModelState.IsValid)
        {
            model.EmpresaId = empresaId.Value;

            _context.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(model);
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
        var empresaId = await GetEmpresaId();
        if (empresaId == null) return Json(new List<object>());

        var eventos = await _context.Agendamentos
            .Include(a => a.Cliente)
            .Include(a => a.Servico)
            .Where(a => a.EmpresaId == empresaId)
            .ToListAsync();

        var lista = eventos.Select(a => new
        {
            id = a.Id,
            title = a.Cliente.Nome + " - " + a.Servico.Nome,
            start = a.DataHora,
            end = a.DataHora.AddMinutes(a.Servico.DuracaoMinutos)
        });

        return Json(lista);
    }
}