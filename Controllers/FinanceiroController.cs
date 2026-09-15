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
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("financeiro")]
    [Authorize(Roles = "Empresa")]
    [TypeFilter(typeof(RequerPlanoFinanceiroFilter))]
    public class FinanceiroController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFinanceiroService _financeiroService;

        public FinanceiroController(
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

        // =========================
        // DASHBOARD FINANCEIRO
        // =========================
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(DateTime? dataInicial, DateTime? dataFinal)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var inicio = (dataInicial ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var fimExclusivo = (dataFinal ?? DateTime.Today).Date.AddDays(1);

            // Receitas/Despesas/Lucro: apurados no período (líquido de estornos).
            var receitasEntradas = await SomarMovimentacoesAsync(
                empresaId.Value, OrigemMovimentacao.ContaReceber, TipoMovimentacao.Entrada, inicio, fimExclusivo);

            var receitasSaidas = await SomarMovimentacoesAsync(
                empresaId.Value, OrigemMovimentacao.ContaReceber, TipoMovimentacao.Saida, inicio, fimExclusivo);

            var despesasSaidas = await SomarMovimentacoesAsync(
                empresaId.Value, OrigemMovimentacao.ContaPagar, TipoMovimentacao.Saida, inicio, fimExclusivo);

            var despesasEntradas = await SomarMovimentacoesAsync(
                empresaId.Value, OrigemMovimentacao.ContaPagar, TipoMovimentacao.Entrada, inicio, fimExclusivo);

            var receitas = receitasEntradas - receitasSaidas;
            var despesas = despesasSaidas - despesasEntradas;

            // Contas a receber/pagar em aberto e saldo de caixa: posição atual
            // (não são presos ao período, é uma "foto" de agora).
            var contasAReceberAberto = await _context.ContasReceber
                .Where(c =>
                    c.EmpresaId == empresaId &&
                    (c.Status == StatusConta.Pendente || c.Status == StatusConta.Parcial))
                .SumAsync(c => (decimal?)(c.ValorPrevisto - c.ValorRecebido)) ?? 0;

            var contasAPagarAberto = await _context.ContasPagar
                .Where(c =>
                    c.EmpresaId == empresaId &&
                    (c.Status == StatusConta.Pendente || c.Status == StatusConta.Parcial))
                .SumAsync(c => (decimal?)(c.ValorPrevisto - c.ValorPago)) ?? 0;

            var totalEntradas = await _context.MovimentacoesFinanceiras
                .Where(m => m.EmpresaId == empresaId && m.Tipo == TipoMovimentacao.Entrada)
                .SumAsync(m => (decimal?)m.Valor) ?? 0;

            var totalSaidas = await _context.MovimentacoesFinanceiras
                .Where(m => m.EmpresaId == empresaId && m.Tipo == TipoMovimentacao.Saida)
                .SumAsync(m => (decimal?)m.Valor) ?? 0;

            var vm = new DashboardFinanceiroViewModel
            {
                DataInicial = inicio,
                DataFinal = fimExclusivo.AddDays(-1),
                Receitas = receitas,
                Despesas = despesas,
                Lucro = receitas - despesas,
                ContasAReceberAberto = contasAReceberAberto,
                ContasAPagarAberto = contasAPagarAberto,
                SaldoCaixa = totalEntradas - totalSaidas
            };

            return View(vm);
        }

        // =========================
        // SINCRONIZAR AGENDAMENTOS EXISTENTES (BACKFILL)
        // =========================
        [HttpPost("sincronizar-agendamentos")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SincronizarAgendamentos()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var quantidade = await _financeiroService.SincronizarAgendamentosExistentesAsync(empresaId.Value);

            ToastHelper.Success(
                TempData,
                quantidade > 0
                    ? $"{quantidade} agendamento(s) sincronizado(s) com o financeiro!"
                    : "Todos os agendamentos já estavam sincronizados.");

            return RedirectToAction(nameof(Dashboard));
        }

        private async Task<decimal> SomarMovimentacoesAsync(
            int empresaId,
            OrigemMovimentacao origem,
            TipoMovimentacao tipo,
            DateTime inicio,
            DateTime fimExclusivo)
        {
            return await _context.MovimentacoesFinanceiras
                .Where(m =>
                    m.EmpresaId == empresaId &&
                    m.Origem == origem &&
                    m.Tipo == tipo &&
                    m.DataMovimento >= inicio &&
                    m.DataMovimento < fimExclusivo)
                .SumAsync(m => (decimal?)m.Valor) ?? 0;
        }

        // =========================
        // CAIXA (EXTRATO)
        // =========================
        [HttpGet("caixa")]
        public async Task<IActionResult> Caixa(
            DateTime? dataInicial,
            DateTime? dataFinal,
            TipoMovimentacao? tipo,
            int page = 1)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            const int pageSize = 20;

            var query = _context.MovimentacoesFinanceiras
                .Include(m => m.Categoria)
                .Include(m => m.Usuario)
                .Where(m => m.EmpresaId == empresaId);

            if (dataInicial.HasValue)
            {
                query = query.Where(m => m.DataMovimento >= dataInicial.Value);
            }

            if (dataFinal.HasValue)
            {
                query = query.Where(m => m.DataMovimento <= dataFinal.Value.AddDays(1));
            }

            if (tipo.HasValue)
            {
                query = query.Where(m => m.Tipo == tipo.Value);
            }

            query = query.OrderByDescending(m => m.DataMovimento);

            var totalItems = await query.CountAsync();

            var lista = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalEntradas = await _context.MovimentacoesFinanceiras
                .Where(m => m.EmpresaId == empresaId && m.Tipo == TipoMovimentacao.Entrada)
                .SumAsync(m => (decimal?)m.Valor) ?? 0;

            var totalSaidas = await _context.MovimentacoesFinanceiras
                .Where(m => m.EmpresaId == empresaId && m.Tipo == TipoMovimentacao.Saida)
                .SumAsync(m => (decimal?)m.Valor) ?? 0;

            ViewBag.SaldoCaixa = totalEntradas - totalSaidas;
            ViewBag.DataInicial = dataInicial;
            ViewBag.DataFinal = dataFinal;
            ViewBag.Tipo = tipo;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(lista);
        }

        // =========================
        // RELATÓRIOS BÁSICOS
        // =========================
        [HttpGet("relatorios")]
        public async Task<IActionResult> Relatorios(DateTime? dataInicial, DateTime? dataFinal)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var inicio = (dataInicial ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var fimExclusivo = (dataFinal ?? DateTime.Today).Date.AddDays(1);

            var receitasPorCategoria = await _context.MovimentacoesFinanceiras
                .Include(m => m.Categoria)
                .Where(m =>
                    m.EmpresaId == empresaId &&
                    m.Origem == OrigemMovimentacao.ContaReceber &&
                    m.Tipo == TipoMovimentacao.Entrada &&
                    m.DataMovimento >= inicio && m.DataMovimento < fimExclusivo)
                .GroupBy(m => m.Categoria != null ? m.Categoria.Nome : "Sem categoria")
                .Select(g => new RelatorioCategoriaItemViewModel
                {
                    CategoriaNome = g.Key,
                    Total = g.Sum(m => m.Valor)
                })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            var despesasPorCategoria = await _context.MovimentacoesFinanceiras
                .Include(m => m.Categoria)
                .Where(m =>
                    m.EmpresaId == empresaId &&
                    m.Origem == OrigemMovimentacao.ContaPagar &&
                    m.Tipo == TipoMovimentacao.Saida &&
                    m.DataMovimento >= inicio && m.DataMovimento < fimExclusivo)
                .GroupBy(m => m.Categoria != null ? m.Categoria.Nome : "Sem categoria")
                .Select(g => new RelatorioCategoriaItemViewModel
                {
                    CategoriaNome = g.Key,
                    Total = g.Sum(m => m.Valor)
                })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            var comissoesPorFuncionario = await _context.AgendamentosFuncionarios
                .Include(af => af.Funcionario)
                .Where(af =>
                    af.Agendamento.EmpresaId == empresaId &&
                    af.ValorComissao != null &&
                    af.Agendamento.DataHora >= inicio && af.Agendamento.DataHora < fimExclusivo)
                .GroupBy(af => af.Funcionario.Nome)
                .Select(g => new RelatorioComissaoItemViewModel
                {
                    FuncionarioNome = g.Key,
                    TotalComissao = g.Sum(af => af.ValorComissao!.Value),
                    Quantidade = g.Count()
                })
                .OrderByDescending(x => x.TotalComissao)
                .ToListAsync();

            var vm = new RelatorioFinanceiroViewModel
            {
                DataInicial = inicio,
                DataFinal = fimExclusivo.AddDays(-1),
                ReceitasPorCategoria = receitasPorCategoria,
                DespesasPorCategoria = despesasPorCategoria,
                ComissoesPorFuncionario = comissoesPorFuncionario
            };

            return View(vm);
        }
    }
}
