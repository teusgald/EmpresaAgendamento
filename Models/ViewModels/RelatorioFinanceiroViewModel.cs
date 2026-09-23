using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class RelatorioCategoriaItemViewModel
    {
        public string CategoriaNome { get; set; } = null!;
        public decimal Total { get; set; }
    }

    public class RelatorioComissaoItemViewModel
    {
        public string FuncionarioNome { get; set; } = null!;
        public decimal TotalComissao { get; set; }
        public int Quantidade { get; set; }
    }

    public class RelatorioFinanceiroViewModel
    {
        public DateTime DataInicial { get; set; }
        public DateTime DataFinal { get; set; }

        public List<RelatorioCategoriaItemViewModel> ReceitasPorCategoria { get; set; } = new();
        public List<RelatorioCategoriaItemViewModel> DespesasPorCategoria { get; set; } = new();
        public List<RelatorioComissaoItemViewModel> ComissoesPorFuncionario { get; set; } = new();
    }

    // Linha do relatório detalhado (impressão) — já formatada para exibição,
    // uma linha por movimentação filtrada.
    public class RelatorioDetalhadoItemViewModel
    {
        public DateTime Data { get; set; }
        public string? Descricao { get; set; }
        public string? Cliente { get; set; }
        public string? Categoria { get; set; }
        public string? FormaPagamento { get; set; }
        public decimal Entrada { get; set; }
        public decimal Saida { get; set; }
    }

    // Modelo da página de visualização/impressão do relatório financeiro
    // detalhado. Totais são sempre calculados só sobre Itens (já filtrados).
    public class RelatorioDetalhadoViewModel
    {
        public Empresa Empresa { get; set; } = null!;

        public DateTime DataInicial { get; set; }
        public DateTime DataFinal { get; set; }
        public DateTime DataGeracao { get; set; }

        // Descrições dos filtros aplicados, só para exibir no cabeçalho
        // (ex.: "Tipo: Entrada", "Cliente: João Silva").
        public string? TipoDescricao { get; set; }
        public string? CategoriaDescricao { get; set; }
        public string? FormaPagamentoDescricao { get; set; }
        public string? FuncionarioDescricao { get; set; }
        public string? ClienteDescricao { get; set; }

        // Valores "crus" dos filtros aplicados — usados pela tela de resultados
        // (RelatorioDetalhado.cshtml) para repopular o formulário e para montar
        // o link "Gerar relatório para impressão" com a mesma querystring.
        public TipoMovimentacao? TipoSelecionado { get; set; }
        public int? CategoriaIdSelecionada { get; set; }
        public FormaPagamento? FormaPagamentoSelecionada { get; set; }
        public int? FuncionarioIdSelecionado { get; set; }
        public int? ClienteIdSelecionado { get; set; }

        public List<RelatorioDetalhadoItemViewModel> Itens { get; set; } = new();

        public decimal TotalEntradas { get; set; }
        public decimal TotalSaidas { get; set; }
        public decimal Saldo => TotalEntradas - TotalSaidas;
    }
}
