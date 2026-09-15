namespace EmpresaAgendamento.Models.ViewModels
{
    public class ComissaoResumoViewModel
    {
        public int FuncionarioId { get; set; }

        public string FuncionarioNome { get; set; } = null!;

        public decimal TotalPendente { get; set; }

        public int QuantidadeAgendamentos { get; set; }
    }
}
