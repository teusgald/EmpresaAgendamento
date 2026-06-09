using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Empresa")]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClientesController(
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

            int pageSize = 10;

            var query = _context.Clientes
                .Where(c => c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                .OrderBy(c => c.Nome);

            var totalItems = await query.CountAsync();

            var lista = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(lista);
        }
        catch (Exception ex)
        {
            ToastHelper.Error(TempData, "Erro ao carregar clientes.");
            // opcional: log ex.Message
            return RedirectToAction("Index", "Home");
        }
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

            ToastHelper.Success(TempData, "Cliente cadastrado com sucesso.");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
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
            ToastHelper.Error(TempData, "Erro ao alterar status do cliente.");
            return RedirectToAction(nameof(Index));
        }
    }
}