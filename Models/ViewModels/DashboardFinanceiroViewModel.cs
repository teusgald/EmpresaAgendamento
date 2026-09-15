namespace EmpresaAgendamento.Models.ViewModels
{
    public class DashboardFinanceiroViewModel
    {
        public DateTime DataInicial { get; set; }
        public DateTime DataFinal { get; set; }

        public decimal Receitas { get; set; }
        public decimal Despesas { get; set; }
        public decimal Lucro { get; set; }

        public decimal ContasAReceberAberto { get; set; }
        public decimal ContasAPagarAberto { get; set; }

        public decimal SaldoCaixa { get; set; }
    }
}
