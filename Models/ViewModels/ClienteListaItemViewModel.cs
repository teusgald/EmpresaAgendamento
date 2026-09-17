namespace EmpresaAgendamento.Models.ViewModels
{
    public class ClienteListaItemViewModel
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public bool Ativo { get; set; }

        public int TotalAgendamentos { get; set; }
        public DateTime? UltimaVisita { get; set; }
        public string? PlanoAtivo { get; set; }
        public decimal GastoTotal { get; set; }
    }
}
