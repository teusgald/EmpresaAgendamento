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
}
