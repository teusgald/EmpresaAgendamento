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
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> Dashboard(DateTime? dataInicial, DateTime? dataFinal)
        {
            try
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar dashboard financeiro.");
                return View(new DashboardFinanceiroViewModel());
            }
        }

        // =========================
        // SINCRONIZAR AGENDAMENTOS EXISTENTES (BACKFILL)
        // =========================
        [HttpPost("sincronizar-agendamentos")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Editar" })]
        public async Task<IActionResult> SincronizarAgendamentos()
        {
            try
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao sincronizar agendamentos.");
                return RedirectToAction(nameof(Dashboard));
            }
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
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> Caixa(
            DateTime? dataInicial,
            DateTime? dataFinal,
            TipoMovimentacao? tipo,
            int page = 1)
        {
            try
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar caixa.");
                return View(new List<MovimentacaoFinanceira>());
            }
        }

        // =========================
        // IMPORTAR EXTRATO (OFX)
        // =========================

        [HttpGet("importar-extrato")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> ImportarExtrato()
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                return View();
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao abrir importação de extrato.");
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpPost("importar-extrato")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> ImportarExtrato(IFormFile arquivo)
        {
            try
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

                List<OfxTransacao> transacoes;

                try
                {
                    transacoes = OfxParser.Parse(conteudo);
                }
                catch
                {
                    ToastHelper.Error(TempData, "Não foi possível ler esse arquivo. Confirme que é um extrato OFX válido.");
                    return View();
                }

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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao importar extrato.");
                return RedirectToAction(nameof(Caixa));
            }
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
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Criar" })]
        public async Task<IActionResult> ConfirmarImportacao(List<ImportacaoExtratoItemViewModel> itens)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                var usuarioId = _userManager.GetUserId(User);

                // CategoriaId vem do form (item por item): nunca confiar sem checar
                // que a categoria pertence a essa empresa, senão um POST manipulado
                // linkaria a movimentação a uma categoria de outra empresa.
                var categoriaIdsValidas = (await _context.CategoriasFinanceiras
                    .Where(c => c.EmpresaId == empresaId)
                    .Select(c => c.Id)
                    .ToListAsync())
                    .ToHashSet();

                var importados = 0;
                var ignorados = 0;
                var erros = new List<string>();

                foreach (var item in itens ?? new List<ImportacaoExtratoItemViewModel>())
                {
                    if (item.CategoriaId.HasValue && !categoriaIdsValidas.Contains(item.CategoriaId.Value))
                    {
                        item.CategoriaId = null;
                    }

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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao confirmar importação.");
                return RedirectToAction(nameof(Caixa));
            }
        }

        // =========================
        // RELATÓRIOS BÁSICOS
        // =========================
        [HttpGet("relatorios")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> Relatorios(DateTime? dataInicial, DateTime? dataFinal)
        {
            try
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar relatórios.");
                return View(new RelatorioFinanceiroViewModel());
            }
        }

        // Combos usados pelo formulário de filtros do relatório detalhado.
        // Os parâmetros "selecionado" pré-marcam a opção atual quando a tela
        // é reaberta já com filtros aplicados (SelectList com selectedValue).
        private async Task CarregarCombosRelatorioAsync(
            int empresaId,
            int? categoriaSelecionada = null,
            int? funcionarioSelecionado = null,
            int? clienteSelecionado = null)
        {
            ViewBag.CategoriasRelatorio = new SelectList(
                await _context.CategoriasFinanceiras
                    .Where(c => c.EmpresaId == empresaId && c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome", categoriaSelecionada);

            ViewBag.FuncionariosRelatorio = new SelectList(
                await _context.Funcionarios
                    .Where(f => f.EmpresaId == empresaId && f.Ativo)
                    .OrderBy(f => f.Nome)
                    .ToListAsync(),
                "Id", "Nome", funcionarioSelecionado);

            ViewBag.ClientesRelatorio = new SelectList(
                await _context.Clientes
                    .Where(c => c.Ativo && c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome", clienteSelecionado);
        }

        // =========================
        // RELATÓRIO DETALHADO (filtro único — tela e impressão)
        // =========================
        // Fonte única da query/projeção do relatório detalhado: usada tanto pela
        // tela de resultados (RelatorioDetalhado) quanto pela página de impressão
        // (RelatorioDetalhadoImpressao), pra não duplicar o filtro em dois lugares
        // que podem divergir com o tempo.
        private async Task<RelatorioDetalhadoViewModel> MontarRelatorioDetalhadoAsync(
            int empresaId,
            DateTime? dataInicial,
            DateTime? dataFinal,
            TipoMovimentacao? tipo,
            int? categoriaId,
            FormaPagamento? formaPagamento,
            int? funcionarioId,
            int? clienteId)
        {
            var empresa = await _context.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);

            if (empresa == null)
            {
                throw new InvalidOperationException("Empresa não encontrada.");
            }

            var inicio = (dataInicial ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var fimExclusivo = (dataFinal ?? DateTime.Today).Date.AddDays(1);

            var query = _context.MovimentacoesFinanceiras
                .Include(m => m.Categoria)
                .Include(m => m.ContaReceber!).ThenInclude(c => c.Cliente)
                .Include(m => m.ContaReceber!).ThenInclude(c => c.Agendamento)
                .Include(m => m.ContaPagar)
                .Where(m =>
                    m.EmpresaId == empresaId &&
                    m.DataMovimento >= inicio && m.DataMovimento < fimExclusivo)
                .AsQueryable();

            if (tipo.HasValue)
            {
                query = query.Where(m => m.Tipo == tipo.Value);
            }

            if (categoriaId.HasValue)
            {
                query = query.Where(m => m.CategoriaId == categoriaId.Value);
            }

            if (formaPagamento.HasValue)
            {
                query = query.Where(m => m.FormaPagamento == formaPagamento.Value);
            }

            if (clienteId.HasValue)
            {
                query = query.Where(m => m.ContaReceber != null && m.ContaReceber.ClienteId == clienteId.Value);
            }

            if (funcionarioId.HasValue)
            {
                // Entrada (recebimento): funcionário vem do agendamento que gerou a
                // conta a receber. Saída (comissão paga): ContaPagar.FuncionarioId
                // já é preenchido especificamente pra isso (ver Models/ContaPagar.cs).
                query = query.Where(m =>
                    (m.ContaReceber != null && m.ContaReceber.Agendamento != null &&
                        m.ContaReceber.Agendamento.FuncionarioId == funcionarioId.Value) ||
                    (m.ContaPagar != null && m.ContaPagar.FuncionarioId == funcionarioId.Value));
            }

            var movimentacoes = await query
                .OrderBy(m => m.DataMovimento)
                .ToListAsync();

            var itens = movimentacoes
                .Select(m => new RelatorioDetalhadoItemViewModel
                {
                    Data = m.DataMovimento,
                    Descricao = m.Descricao,
                    Cliente = m.ContaReceber?.Cliente?.Nome ?? m.ContaReceber?.Agendamento?.NomeClienteAvulso,
                    Categoria = m.Categoria?.Nome,
                    FormaPagamento = m.FormaPagamento?.ToString(),
                    Entrada = m.Tipo == TipoMovimentacao.Entrada ? m.Valor : 0,
                    Saida = m.Tipo == TipoMovimentacao.Saida ? m.Valor : 0
                })
                .ToList();

            string? categoriaDescricao = categoriaId.HasValue
                ? await _context.CategoriasFinanceiras
                    .Where(c => c.Id == categoriaId.Value && c.EmpresaId == empresaId)
                    .Select(c => c.Nome)
                    .FirstOrDefaultAsync()
                : null;

            string? funcionarioDescricao = funcionarioId.HasValue
                ? await _context.Funcionarios
                    .Where(f => f.Id == funcionarioId.Value && f.EmpresaId == empresaId)
                    .Select(f => f.Nome)
                    .FirstOrDefaultAsync()
                : null;

            string? clienteDescricao = clienteId.HasValue
                ? await _context.Clientes
                    .Where(c => c.Id == clienteId.Value && c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                    .Select(c => c.Nome)
                    .FirstOrDefaultAsync()
                : null;

            return new RelatorioDetalhadoViewModel
            {
                Empresa = empresa,
                DataInicial = inicio,
                DataFinal = fimExclusivo.AddDays(-1),
                DataGeracao = DateTime.Now,
                TipoDescricao = tipo?.ToString(),
                CategoriaDescricao = categoriaDescricao,
                FormaPagamentoDescricao = formaPagamento?.ToString(),
                FuncionarioDescricao = funcionarioDescricao,
                ClienteDescricao = clienteDescricao,
                TipoSelecionado = tipo,
                CategoriaIdSelecionada = categoriaId,
                FormaPagamentoSelecionada = formaPagamento,
                FuncionarioIdSelecionado = funcionarioId,
                ClienteIdSelecionado = clienteId,
                Itens = itens,
                TotalEntradas = itens.Sum(i => i.Entrada),
                TotalSaidas = itens.Sum(i => i.Saida)
            };
        }

        // Tela normal (com layout do portal): filtra e mostra os dados antes de
        // decidir gerar a versão para impressão. Usa a mesma query/projeção de
        // RelatorioDetalhadoImpressao via MontarRelatorioDetalhadoAsync.
        [HttpGet("relatorios/detalhado")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> RelatorioDetalhado(
            DateTime? dataInicial,
            DateTime? dataFinal,
            TipoMovimentacao? tipo,
            int? categoriaId,
            FormaPagamento? formaPagamento,
            int? funcionarioId,
            int? clienteId)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                var vm = await MontarRelatorioDetalhadoAsync(
                    empresaId.Value, dataInicial, dataFinal, tipo, categoriaId, formaPagamento, funcionarioId, clienteId);

                await CarregarCombosRelatorioAsync(empresaId.Value, categoriaId, funcionarioId, clienteId);

                return View(vm);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao gerar relatório detalhado.");
                return RedirectToAction(nameof(Relatorios));
            }
        }

        // Página de impressão (A4, Layout=null): recebe os MESMOS filtros já
        // aplicados na tela de resultados (mesma querystring) — o usuário não
        // preenche o formulário de novo. Query/projeção idênticas à tela normal,
        // via MontarRelatorioDetalhadoAsync.
        [HttpGet("relatorios/detalhado/impressao")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Financeiro", "Visualizar" })]
        public async Task<IActionResult> RelatorioDetalhadoImpressao(
            DateTime? dataInicial,
            DateTime? dataFinal,
            TipoMovimentacao? tipo,
            int? categoriaId,
            FormaPagamento? formaPagamento,
            int? funcionarioId,
            int? clienteId)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                var vm = await MontarRelatorioDetalhadoAsync(
                    empresaId.Value, dataInicial, dataFinal, tipo, categoriaId, formaPagamento, funcionarioId, clienteId);

                return View("RelatorioVisualizacao", vm);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao gerar relatório para impressão.");
                return RedirectToAction(nameof(Relatorios));
            }
        }
    }
}
