using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa")]
    public class EmpresasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmpresasController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Empresas
        public async Task<IActionResult> Index()
        {
            var empresa = await _context.Empresas.FirstOrDefaultAsync();

            if (empresa == null)
            {
                empresa = new Empresa
                {
                    Nome = "",
                    Ativo = true
                };

                _context.Empresas.Add(empresa);
                await _context.SaveChangesAsync();
            }

            return View(empresa);
        }

        // GET: Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null || user.EmpresaId == null)
                return RedirectToAction("Login", "Account");

            var empresaId = user.EmpresaId.Value;

            // CLIENTES
            var totalClientes = await _context.Clientes
                .CountAsync(c => c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

            // AGENDAMENTOS HOJE
            var agendamentosHoje = await _context.Agendamentos
                .Include(a => a.Cliente)
                .Include(a => a.Servico)
                .Where(a => a.EmpresaId == empresaId && a.DataHora.Date == DateTime.Today)
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            // FATURAMENTO MENSAL
            var faturamentoMensal = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a => a.EmpresaId == empresaId &&
                            a.DataHora.Month == DateTime.Today.Month &&
                            a.DataHora.Year == DateTime.Today.Year)
                .SumAsync(a => (decimal?)a.Servico.Preco) ?? 0;

            // FATURAMENTO MÊS ANTERIOR
            var dataAnterior = DateTime.Today.AddMonths(-1);

            var faturamentoMesAnterior = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a => a.EmpresaId == empresaId &&
                            a.DataHora.Month == dataAnterior.Month &&
                            a.DataHora.Year == dataAnterior.Year)
                .SumAsync(a => (decimal?)a.Servico.Preco) ?? 0;

            // CRESCIMENTO
            decimal crescimento = faturamentoMesAnterior == 0
                ? 100
                : ((faturamentoMensal - faturamentoMesAnterior) / faturamentoMesAnterior) * 100;

            // FATURAMENTO POR MÊS
            var faturamentoPorMes = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a => a.EmpresaId == empresaId)
                .GroupBy(a => a.DataHora.Month)
                .Select(g => new { Mes = g.Key, Total = g.Sum(a => a.Servico.Preco) })
                .ToListAsync();

            // SERVIÇOS POPULARES
            var servicosPopulares = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a => a.EmpresaId == empresaId)
                .GroupBy(a => a.Servico.Nome)
                .Select(g => new { Servico = g.Key, Quantidade = g.Count() })
                .OrderByDescending(s => s.Quantidade)
                .Take(6)
                .ToListAsync();

            ViewBag.TotalClientes = totalClientes;
            ViewBag.TotalAgendamentosHoje = agendamentosHoje.Count;
            ViewBag.FaturamentoMensal = faturamentoMensal;
            ViewBag.Crescimento = crescimento;
            ViewBag.AgendamentosHoje = agendamentosHoje;
            ViewBag.FaturamentoPorMes = faturamentoPorMes;
            ViewBag.ServicosPopulares = servicosPopulares;

            return View();
        }

        // POST: Empresas/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Empresa empresa)
        {
            if (!ModelState.IsValid)
                return View(empresa);

            try
            {
                _context.Update(empresa);
                await _context.SaveChangesAsync();

                ViewBag.Mensagem = "Dados atualizados com sucesso!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Empresas.Any(e => e.Id == empresa.Id))
                    return NotFound();

                throw;
            }

            return View(empresa);
        }
    }
}