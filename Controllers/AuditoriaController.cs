using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("auditoria")]
    [Authorize(Roles = "Empresa,Funcionario")]
    [TypeFilter(typeof(RequerGerenteFilter))]
    public class AuditoriaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditoriaController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? busca, int page = 1)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            const int pageSize = 20;

            var query = _context.AuditoriasAcesso
                .Where(a => a.EmpresaId == empresaId)
                .OrderByDescending(a => a.CriadoEm)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                query = query.Where(a =>
                    (a.UsuarioNome != null && a.UsuarioNome.Contains(busca)) ||
                    (a.Detalhe != null && a.Detalhe.Contains(busca)));
            }

            var totalItems = await query.CountAsync();

            var itens = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Busca = busca;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(itens);
        }
    }
}
