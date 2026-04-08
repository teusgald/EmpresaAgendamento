using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa")]
    public class ClientesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClientesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        public async Task<IActionResult> Index()
        {
            var empresaId = await GetEmpresaId();
            if (empresaId == null) return RedirectToAction("Login", "Account");

            int id = empresaId.Value;

            var lista = await _context.Clientes
                .Where(c => c.EmpresaClientes.Any(ec => ec.EmpresaId == id))
                .ToListAsync();

            return View(lista);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Cliente cliente)
        {
            var empresaId = await GetEmpresaId();
            if (empresaId == null) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // 🔥 VINCULAR CLIENTE À EMPRESA
                _context.EmpresaClientes.Add(new EmpresaCliente
                {
                    ClienteId = cliente.Id,
                    EmpresaId = empresaId.Value
                });

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(cliente);
        }
    }
}
