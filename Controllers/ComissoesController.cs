using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("financeiro/comissoes")]
    [Authorize(Roles = "Empresa,Funcionario")]
    [TypeFilter(typeof(RequerGerenteFilter))]
    [TypeFilter(typeof(RequerPlanoFinanceiroFilter))]
    public class ComissoesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFinanceiroService _financeiroService;

        public ComissoesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFinanceiroService financeiroService)
        {
            _context = context;
            _userManager = userManager;
            _financeiroService = financeiroService;
        }

        private async Task<int?> GetEmpresaId()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                return user?.EmpresaId;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            int? funcionarioId,
            bool somentePendentes = true,
            DateTime? dataInicial = null,
            DateTime? dataFinal = null,
            int page = 1)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            const int pageSize = 15;

            var query = _context.AgendamentosFuncionarios
                .Include(af => af.Funcionario)
                .Include(af => af.Agendamento).ThenInclude(a => a.Servico)
                .Include(af => af.Agendamento).ThenInclude(a => a.Cliente)
                .Where(af =>
                    af.Agendamento.EmpresaId == empresaId &&
                    af.ValorComissao != null);

            if (funcionarioId.HasValue)
            {
                query = query.Where(af => af.FuncionarioId == funcionarioId.Value);
            }

            if (somentePendentes)
            {
                query = query.Where(af => af.ValorRecebido == null);
            }

            if (dataInicial.HasValue)
            {
                query = query.Where(af => af.Agendamento.DataHora >= dataInicial.Value);
            }

            if (dataFinal.HasValue)
            {
                query = query.Where(af => af.Agendamento.DataHora <= dataFinal.Value.AddDays(1));
            }

            query = query.OrderByDescending(af => af.Agendamento.DataHora);

            var totalItems = await query.CountAsync();

            var lista = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Resumo por funcionário — só do que está pendente, pra facilitar o "Pagar".
            var resumoPendentes = await _context.AgendamentosFuncionarios
                .Include(af => af.Funcionario)
                .Where(af =>
                    af.Agendamento.EmpresaId == empresaId &&
                    af.ValorComissao != null &&
                    af.ValorComissao > 0 &&
                    af.ValorRecebido == null)
                .GroupBy(af => new { af.FuncionarioId, af.Funcionario.Nome })
                .Select(g => new ComissaoResumoViewModel
                {
                    FuncionarioId = g.Key.FuncionarioId,
                    FuncionarioNome = g.Key.Nome,
                    TotalPendente = g.Sum(x => x.ValorComissao!.Value),
                    QuantidadeAgendamentos = g.Count()
                })
                .OrderByDescending(x => x.TotalPendente)
                .ToListAsync();

            ViewBag.ResumoPendentes = resumoPendentes;

            ViewBag.Funcionarios = new SelectList(
                await _context.Funcionarios
                    .Where(f => f.EmpresaId == empresaId)
                    .OrderBy(f => f.Nome)
                    .ToListAsync(),
                "Id", "Nome");

            ViewBag.FuncionarioId = funcionarioId;
            ViewBag.SomentePendentes = somentePendentes;
            ViewBag.DataInicial = dataInicial;
            ViewBag.DataFinal = dataFinal;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(lista);
        }

        [HttpPost("pagar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pagar(
            int funcionarioId,
            DateTime? dataInicial,
            DateTime? dataFinal,
            FormaPagamento formaPagamento)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var usuarioId = _userManager.GetUserId(User);

            var (sucesso, erro, conta) = await _financeiroService.PagarComissoesPendentesAsync(
                empresaId.Value, funcionarioId, dataInicial, dataFinal, formaPagamento, usuarioId);

            if (!sucesso)
            {
                ToastHelper.Error(TempData, erro ?? "Erro ao pagar comissões.");
            }
            else
            {
                ToastHelper.Success(
                    TempData,
                    $"Comissões pagas com sucesso! Total: {conta!.ValorPago:C}");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
