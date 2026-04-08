using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{

    [Authorize(Roles = "Empresa")]
    public class ServicosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ServicosController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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

            var lista = await _context.Servicos
                .Where(s => s.EmpresaId == empresaId)
                .ToListAsync();

            return View(lista);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Servico servico)
        {
            var empresaId = await GetEmpresaId();
            if (empresaId == null) return RedirectToAction("Login", "Account");

            ModelState.Remove("EmpresaId");

            if (ModelState.IsValid)
            {
                servico.EmpresaId = empresaId.Value;

                _context.Add(servico);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(servico);
        }
    }
}
