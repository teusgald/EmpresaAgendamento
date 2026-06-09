using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
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

    private async Task<ApplicationUser> GetCurrentUserAsync()
    {
        return await _userManager.GetUserAsync(User);
    }

    // 📋 LISTA
    [Route("Index")]
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user.Cliente.Id == null) return RedirectToAction("Login", "ClientesAuth");

        var agendamentos = await _context.Agendamentos
            .AsNoTracking()
            .Include(a => a.Servico)
            .Include(a => a.Empresa)
            .Where(a => a.ClienteId == user.Cliente.Id)
            .OrderByDescending(a => a.DataCriacao)
            .ToListAsync();

        return View(agendamentos);
    }

    // ➕ FORM CRIAR
    [HttpGet("novo")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Empresas = new SelectList(
            await _context.Empresas.Where(e => e.Ativo).AsNoTracking().ToListAsync(),
            "Id", "Nome"
        );

        ViewBag.Servicos = new SelectList(Enumerable.Empty<Servico>(), "Id", "Nome");

        return View();
    }

    // 💾 SALVAR
    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Agendamento agendamento)
    {
        var user = await GetCurrentUserAsync();
        if (user.Cliente.Id == null) return RedirectToAction("Login", "ClientesAuth");

        agendamento.ClienteId = user.Cliente.Id;

        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = new SelectList(await _context.Empresas.Where(e => e.Ativo).ToListAsync(), "Id", "Nome");
            ViewBag.Servicos = new SelectList(await _context.Servicos.Where(s => s.EmpresaId == agendamento.EmpresaId).ToListAsync(), "Id", "Nome");
            return View(agendamento);
        }

        _context.Agendamentos.Add(agendamento);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // ✏️ EDITAR
    [HttpGet("editar/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await GetCurrentUserAsync();
        var agendamento = await _context.Agendamentos.FindAsync(id);

        if (agendamento == null || agendamento.ClienteId != user.Cliente.Id)
            return NotFound();

        ViewBag.Empresas = new SelectList(await _context.Empresas.Where(e => e.Ativo).ToListAsync(), "Id", "Nome", agendamento.EmpresaId);
        ViewBag.Servicos = new SelectList(await _context.Servicos.Where(s => s.EmpresaId == agendamento.EmpresaId).ToListAsync(), "Id", "Nome", agendamento.ServicoId);

        return View(agendamento);
    }

    // 💾 SALVAR EDIÇÃO
    [HttpPost("editar/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Agendamento agendamento)
    {
        var user = await GetCurrentUserAsync();
        if (id != agendamento.Id) return NotFound();

        agendamento.ClienteId = user.Cliente.Id;

        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = new SelectList(await _context.Empresas.Where(e => e.Ativo).ToListAsync(), "Id", "Nome", agendamento.EmpresaId);
            ViewBag.Servicos = new SelectList(await _context.Servicos.Where(s => s.EmpresaId == agendamento.EmpresaId).ToListAsync(), "Id", "Nome", agendamento.ServicoId);
            return View(agendamento);
        }

        _context.Update(agendamento);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // ❌ EXCLUIR
    [HttpGet("excluir/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await GetCurrentUserAsync();

        var agendamento = await _context.Agendamentos
            .Include(a => a.Servico)
            .Include(a => a.Empresa)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.ClienteId == user.Cliente.Id);

        if (agendamento == null) return NotFound();

        return View(agendamento);
    }

    [HttpPost("excluir/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await GetCurrentUserAsync();
        var agendamento = await _context.Agendamentos.FindAsync(id);

        if (agendamento != null && agendamento.ClienteId == user.Cliente.Id)
        {
            _context.Remove(agendamento);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // 🔄 AJAX: Buscar serviços por empresa
    [HttpGet("servicos/{empresaId}")]
    public async Task<IActionResult> GetServicosByEmpresa(int empresaId)
    {
        var servicos = await _context.Servicos
            .Where(s => s.EmpresaId == empresaId)
            .Select(s => new { s.Id, s.Nome })
            .ToListAsync();

        return Json(servicos);
    }

    // 🔒 LOGOUT
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "ClientesAuth");
    }
}