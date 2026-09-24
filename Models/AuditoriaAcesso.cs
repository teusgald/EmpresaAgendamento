using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    // Registro de mudança em dado sensível (hoje só Cliente) — quem fez,
    // quando, o quê. "Entidade" é string livre pra dar pra reaproveitar em
    // outro cadastro sensível no futuro sem precisar de tabela nova.
    public class AuditoriaAcesso
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        [Required]
        public string UsuarioId { get; set; } = null!;

        // Snapshot do nome — sobrevive mesmo se o funcionário for desligado
        // e o cadastro dele apagado depois.
        [StringLength(150)]
        public string? UsuarioNome { get; set; }

        [Required]
        [StringLength(40)]
        public string Entidade { get; set; } = null!;

        public int EntidadeId { get; set; }

        [Required]
        [StringLength(30)]
        public string Acao { get; set; } = null!;

        [StringLength(500)]
        public string? Detalhe { get; set; }

        public DateTime CriadoEm { get; set; } = DateTime.Now;
    }
}
