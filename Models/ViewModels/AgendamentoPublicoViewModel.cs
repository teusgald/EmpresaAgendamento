namespace EmpresaAgendamento.Models.ViewModels
{
    public class AgendamentoPublicoViewModel
    {
        public int EmpresaId { get; set; }

        public int ServicoId { get; set; }

        public int? FuncionarioId { get; set; }

        public string Nome { get; set; }

        public string Telefone { get; set; }

        public DateTime DataHora { get; set; }
    }
}
