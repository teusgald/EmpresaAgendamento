namespace EmpresaAgendamento.Models
{
    public class EmpresaFoto
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        public string Url { get; set; } = null!;

        public DateTime DataUpload { get; set; } = DateTime.UtcNow;
    }
}
