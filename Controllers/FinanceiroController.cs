using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using EmpresaAgendamento.Services.Ofx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("financeiro")]
    [Authorize(Roles = "Empresa,Funcionario")]
    [TypeFilter(typeof(RequerGerenteFilter))]
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
        // IMPORTAR EXTRATO (OFX)
        // =========================

        [HttpGet("importar-extrato")]
        public async Task<IActionResult> ImportarExtrato()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            return View();
        }

        [HttpPost("importar-extrato")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarExtrato(IFormFile arquivo)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            if (arquivo == null || arquivo.Length == 0)
            {
                ToastHelper.Error(TempData, "Selecione um arquivo OFX.");
                return View();
            }

            string conteudo;

            using (var leitor = new StreamReader(arquivo.OpenReadStream(), System.Text.Encoding.Latin1))
            {
                conteudo = await leitor.ReadToEndAsync();
            }

            var transacoes = OfxParser.Parse(conteudo);

            if (transacoes.Count == 0)
            {
                ToastHelper.Error(TempData, "Não encontramos lançamentos nesse arquivo. Confirme que é um extrato OFX válido.");
                return View();
            }

            var fitIdsJaImportados = await _context.MovimentacoesFinanceiras
                .Where(m => m.EmpresaId == empresaId && m.ReferenciaExterna != null)
                .Select(m => m.ReferenciaExterna!)
                .ToListAsync();

            var contasReceberAbertas = await _context.ContasReceber
                .Where(c => c.EmpresaId == empresaId && (c.Status == StatusConta.Pendente || c.Status == StatusConta.Parcial))
                .OrderBy(c => c.DataVencimento)
                .ToListAsync();

            var contasPagarAbertas = await _context.ContasPagar
                .Where(c => c.EmpresaId == empresaId && (c.Status == StatusConta.Pendente || c.Status == StatusConta.Parcial))
                .OrderBy(c => c.DataVencimento)
                .ToListAsync();

            var itens = new List<ImportacaoExtratoItemViewModel>();

            foreach (var transacao in transacoes.OrderBy(t => t.Data))
            {
                var jaImportado = !string.IsNullOrWhiteSpace(transacao.FitId) && fitIdsJaImportados.Contains(transacao.FitId);

                var item = new ImportacaoExtratoItemViewModel
                {
                    Data = transacao.Data,
                    Valor = transacao.Valor,
                    Descricao = transacao.Descricao,
                    FitId = transacao.FitId,
                    JaImportado = jaImportado
                };

                if (jaImportado)
                {
                    item.Acao = "ignorar";
                }
                else if (transacao.Valor > 0)
                {
                    MontarSugestaoConciliacao(
                        item,
                        contasReceberAbertas,
                        transacao.Data,
                        transacao.Valor,
                        c => c.Id,
                        c => c.Descricao,
                        c => c.ValorPrevisto - c.ValorRecebido,
                        c => c.DataVencimento);
                }
                else if (transacao.Valor < 0)
                {
                    MontarSugestaoConciliacao(
                        item,
                        contasPagarAbertas,
                        transacao.Data,
                        -transacao.Valor,
                        c => c.Id,
                        c => c.Descricao,
                        c => c.ValorPrevisto - c.ValorPago,
                        c => c.DataVencimento);
                }
                else
                {
                    item.Acao = "ignorar";
                }

                itens.Add(item);
            }

            ViewBag.Categorias = new SelectList(
                await _context.CategoriasFinanceiras
                    .Where(c => c.EmpresaId == empresaId && c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");

            return View("RevisarImportacao", itens);
        }

        // Preenche as opções do <select> de conciliação (todas as contas em
        // aberto do tipo certo) e já pré-seleciona a melhor candidata — só
        // quando o valor bate exatinho com o saldo em aberto (pra não arriscar
        // conciliar com a conta errada por aproximação).
        private static void MontarSugestaoConciliacao<TConta>(
            ImportacaoExtratoItemViewModel item,
            List<TConta> contasAbertas,
            DateTime dataTransacao,
            decimal valorAbsoluto,
            Func<TConta, int> idSelector,
            Func<TConta, string> descricaoSelector,
            Func<TConta, decimal> saldoSelector,
            Func<TConta, DateTime> vencimentoSelector)
        {
            var melhor = contasAbertas
                .Where(c => Math.Abs(saldoSelector(c) - valorAbsoluto) < 0.01m)
                .OrderBy(c => Math.Abs((vencimentoSelector(c) - dataTransacao).TotalDays))
                .FirstOrDefault();

            item.ContasDisponiveis = contasAbertas
                .Select(c => new SelectListItem(
                    $"{descricaoSelector(c)} — R$ {saldoSelector(c):N2} (venc. {vencimentoSelector(c):dd/MM/yyyy})",
                    idSelector(c).ToString(),
                    melhor != null && idSelector(c).Equals(idSelector(melhor))))
                .ToList();

            item.Acao = melhor != null ? "conciliar" : "novo";
            item.ContaSelecionadaId = melhor != null ? idSelector(melhor) : null;
        }

        [HttpPost("confirmar-importacao")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarImportacao(List<ImportacaoExtratoItemViewModel> itens)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var usuarioId = _userManager.GetUserId(User);

            var importados = 0;
            var ignorados = 0;
            var erros = new List<string>();

            foreach (var item in itens ?? new List<ImportacaoExtratoItemViewModel>())
            {
                if (item.Acao == "ignorar")
                {
                    ignorados++;
                    continue;
                }

                // Reconfere no banco (não confia só no que veio do form) — evita
                // duplicar se o mesmo extrato foi confirmado em outra aba/tentativa.
                if (!string.IsNullOrWhiteSpace(item.FitId))
                {
                    var jaExiste = await _context.MovimentacoesFinanceiras
                        .AnyAsync(m => m.EmpresaId == empresaId && m.ReferenciaExterna == item.FitId);

                    if (jaExiste)
                    {
                        ignorados++;
                        continue;
                    }
                }

                if (item.Acao == "conciliar" && item.ContaSelecionadaId.HasValue)
                {
                    if (item.Valor > 0)
                    {
                        var (sucesso, erro, _) = await _financeiroService.RegistrarRecebimentoAsync(
                            item.ContaSelecionadaId.Value, empresaId.Value, item.Valor,
                            FormaPagamento.Outro, usuarioId, item.Data, item.FitId);

                        if (sucesso) importados++;
                        else erros.Add($"{item.Descricao}: {erro}");
                    }
                    else
                    {
                        var (sucesso, erro, _) = await _financeiroService.RegistrarPagamentoAsync(
                            item.ContaSelecionadaId.Value, empresaId.Value, -item.Valor,
                            FormaPagamento.Outro, usuarioId, item.Data, item.FitId);

                        if (sucesso) importados++;
                        else erros.Add($"{item.Descricao}: {erro}");
                    }
                }
                else
                {
                    // Sem conta pra conciliar — lançamento avulso direto no caixa.
                    _context.MovimentacoesFinanceiras.Add(new MovimentacaoFinanceira
                    {
                        EmpresaId = empresaId.Value,
                        Tipo = item.Valor >= 0 ? TipoMovimentacao.Entrada : TipoMovimentacao.Saida,
                        Origem = OrigemMovimentacao.Ajuste,
                        Valor = Math.Abs(item.Valor),
                        DataMovimento = item.Data,
                        Status = StatusMovimentacao.Confirmada,
                        Descricao = item.Descricao,
                        CategoriaId = item.CategoriaId,
                        ReferenciaExterna = item.FitId,
                        UsuarioId = usuarioId
                    });

                    await _context.SaveChangesAsync();
                    importados++;
                }
            }

            if (erros.Any())
            {
                ToastHelper.Warning(TempData, $"{importados} lançamento(s) importado(s), mas {erros.Count} falharam: {string.Join("; ", erros)}");
            }
            else
            {
                ToastHelper.Success(
                    TempData,
                    $"{importados} lançamento(s) importado(s) com sucesso!" +
                    (ignorados > 0 ? $" ({ignorados} já tinham sido importados antes e foram ignorados.)" : ""));
            }

            return RedirectToAction(nameof(Caixa));
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
