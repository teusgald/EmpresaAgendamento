using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    public class Avaliacao
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;

        [Range(1, 5)]
        public int Nota { get; set; }

        [StringLength(500)]
        public string? Comentario { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public DateTime? DataAtualizacao { get; set; }
    }
}
