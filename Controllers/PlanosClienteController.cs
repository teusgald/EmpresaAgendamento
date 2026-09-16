using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("Cliente/Planos")]
    [Authorize(Roles = "Cliente")]
    public class PlanosClienteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlanosClienteController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetClienteId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.ClienteId;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var clienteId = await GetClienteId();

            if (clienteId == null)
                return RedirectToAction("Login", "ClientesAuth");

            var meusPlanos = await _context.AssinaturasPlanoServico
                .Include(a => a.PlanoServico).ThenInclude(p => p.Empresa)
                .Where(a => a.ClienteId == clienteId)
                .OrderByDescending(a => a.Status == StatusAssinaturaPlano.Ativa)
                .ThenByDescending(a => a.DataInicio)
                .ToListAsync();

            foreach (var assinatura in meusPlanos.Where(a => a.Status == StatusAssinaturaPlano.Ativa))
            {
                assinatura.AtualizarPeriodoSeNecessario();
            }

            await _context.SaveChangesAsync();

            var idsJaSolicitados = meusPlanos
                .Where(a => a.Status != StatusAssinaturaPlano.Cancelada)
                .Select(a => a.PlanoServicoId)
                .ToHashSet();

            var planosDisponiveis = await _context.PlanosServico
                .Include(p => p.Empresa)
                .Include(p => p.Servicos).ThenInclude(x => x.Servico)
                .Where(p => p.Ativo && p.Empresa.Ativo && !idsJaSolicitados.Contains(p.Id))
                .OrderBy(p => p.Empresa.Nome).ThenBy(p => p.Nome)
                .ToListAsync();

            ViewBag.MeusPlanos = meusPlanos;

            return View(planosDisponiveis);
        }

        [HttpPost("Solicitar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitar(int planoServicoId)
        {
            var clienteId = await GetClienteId();

            if (clienteId == null)
                return RedirectToAction("Login", "ClientesAuth");

            var plano = await _context.PlanosServico
                .FirstOrDefaultAsync(p => p.Id == planoServicoId && p.Ativo);

            if (plano == null)
            {
                ToastHelper.Error(TempData, "Plano não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            var jaAssinante = await _context.AssinaturasPlanoServico
                .AnyAsync(a =>
                    a.PlanoServicoId == planoServicoId &&
                    a.ClienteId == clienteId &&
                    (a.Status == StatusAssinaturaPlano.Ativa || a.Status == StatusAssinaturaPlano.Pendente));

            if (jaAssinante)
            {
                ToastHelper.Warning(TempData, "Você já solicitou ou já assina este plano.");
                return RedirectToAction(nameof(Index));
            }

            _context.AssinaturasPlanoServico.Add(new AssinaturaPlanoServico
            {
                PlanoServicoId = planoServicoId,
                ClienteId = clienteId.Value,
                Status = StatusAssinaturaPlano.Pendente
            });

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Solicitação enviada! A empresa vai confirmar o pagamento e ativar seu plano.");
            return RedirectToAction(nameof(Index));
        }
    }
}
