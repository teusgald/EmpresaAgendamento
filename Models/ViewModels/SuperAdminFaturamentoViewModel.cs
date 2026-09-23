namespace EmpresaAgendamento.Models.ViewModels
{
    public class SuperAdminFaturamentoViewModel
    {
        public string Periodo { get; set; } = "mes";

        public DateTime DataInicial { get; set; }
        public DateTime DataFinalExclusiva { get; set; }

        public int TotalFaturas { get; set; }
        public decimal ValorTotal { get; set; }
    }
}
