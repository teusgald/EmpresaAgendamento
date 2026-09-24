using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("simpli-ai")]
    [Authorize(Roles = "Empresa,Funcionario")]
    public class SimpliAiController : Controller
    {
        private const int MensagensDeHistorico = 10;
        private const int MensagensDeHistoricoPagina = 30;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAiAssistantService _aiAssistantService;

        public SimpliAiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAiAssistantService aiAssistantService)
        {
            _context = context;
            _userManager = userManager;
            _aiAssistantService = aiAssistantService;
        }

        public class MensagemRequest
        {
            public string Mensagem { get; set; } = "";
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user?.EmpresaId == null)
                return RedirectToAction("Login", "EmpresaAuth");

            var empresaId = user.EmpresaId.Value;

            var liberado = await _context.Empresas
                .Where(e => e.Id == empresaId)
                .Select(e => e.SimpliAiBetaAtivo || (e.Plano != null && e.Plano.PermiteIA))
                .FirstOrDefaultAsync();

            if (!liberado)
            {
                ToastHelper.Warning(TempData, "O Simpli AI não está disponível no seu plano ainda.");
                return RedirectToAction("Dashboard", "Empresas");
            }

            var historico = await _context.InteracoesIA
                .Where(i => i.EmpresaId == empresaId)
                .OrderByDescending(i => i.Id)
                .Take(MensagensDeHistoricoPagina)
                .OrderBy(i => i.Id)
                .ToListAsync();

            return View(historico);
        }

        [HttpPost("mensagem")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mensagem([FromBody] MensagemRequest request)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user?.EmpresaId == null)
                return Unauthorized();

            var empresaId = user.EmpresaId.Value;

            if (string.IsNullOrWhiteSpace(request?.Mensagem))
                return BadRequest(new { erro = "Digite uma mensagem." });

            var empresa = await _context.Empresas
                .Include(e => e.Plano)
                .Where(e => e.Id == empresaId)
                .Select(e => new { e.SimpliAiBetaAtivo, PermiteIA = e.Plano != null && e.Plano.PermiteIA, LimiteIAMes = e.Plano != null ? e.Plano.LimiteIAMes : 0 })
                .FirstOrDefaultAsync();

            if (empresa == null || (!empresa.SimpliAiBetaAtivo && !empresa.PermiteIA))
                return StatusCode(403, new { erro = "O Simpli AI não está disponível no seu plano." });

            if (empresa.LimiteIAMes > 0)
            {
                var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var fimMes = inicioMes.AddMonths(1);

                var totalNoMes = await _context.InteracoesIA.CountAsync(i =>
                    i.EmpresaId == empresaId &&
                    i.Role == "user" &&
                    i.CriadoEm >= inicioMes &&
                    i.CriadoEm < fimMes);

                if (totalNoMes >= empresa.LimiteIAMes)
                {
                    return StatusCode(429, new
                    {
                        erro = $"Seu plano permite até {empresa.LimiteIAMes} mensagem(ns) por mês no Simpli AI. Faça upgrade do plano para continuar."
                    });
                }
            }

            var historico = await _context.InteracoesIA
                .Where(i => i.EmpresaId == empresaId)
                .OrderByDescending(i => i.Id)
                .Take(MensagensDeHistorico)
                .OrderBy(i => i.Id)
                .Select(i => new { i.Role, i.Mensagem })
                .ToListAsync();

            var resposta = await _aiAssistantService.ConversarAsync(
                empresaId,
                request.Mensagem,
                historico.Select(h => (h.Role, h.Mensagem)).ToList());

            _context.InteracoesIA.Add(new InteracaoIA { EmpresaId = empresaId, Role = "user", Mensagem = request.Mensagem });
            _context.InteracoesIA.Add(new InteracaoIA { EmpresaId = empresaId, Role = "model", Mensagem = resposta });
            await _context.SaveChangesAsync();

            return Json(new { resposta });
        }
    }
}
