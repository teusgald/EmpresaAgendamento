using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario,Cliente")]
    [Route("notificacoes")]
    public class NotificacoesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificacoesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Index", "Home");
            }

            List<Notificacao> lista;

            if (user.ClienteId.HasValue)
            {
                lista = await _context.Notificacoes
                    .Where(n => n.ClienteId == user.ClienteId)
                    .OrderByDescending(n => n.DataCriacao)
                    .Take(50)
                    .ToListAsync();
            }
            else if (user.EmpresaId.HasValue)
            {
                lista = await _context.Notificacoes
                    .Where(n => n.EmpresaId == user.EmpresaId && n.ClienteId == null)
                    .OrderByDescending(n => n.DataCriacao)
                    .Take(50)
                    .ToListAsync();
            }
            else
            {
                lista = new List<Notificacao>();
            }

            // Abrir a tela já marca tudo como lido — some do contador do sino.
            // Guarda quem estava não-lida ANTES de marcar, só pra destacar
            // essas na tela nesta visita (senão o "Nova" nunca apareceria,
            // já que a lista mutada em memória vai pra view já toda lida).
            var idsRecemLidos = lista.Where(n => !n.Lida).Select(n => n.Id).ToHashSet();

            if (idsRecemLidos.Any())
            {
                foreach (var notificacao in lista.Where(n => idsRecemLidos.Contains(n.Id)))
                {
                    notificacao.Lida = true;
                    notificacao.DataLeitura = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }

            ViewBag.IdsRecemLidos = idsRecemLidos;
            ViewData["Title"] = "Notificações";

            return View(lista);
        }
    }
}
