using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    // Solicitação de atendimento (fala com a equipe do Simpli Time) — cada
    // usuário vê só as próprias, com status pra saber se já foi resolvida.
    public class Atendimento
    {
        public int Id { get; set; }

        public string? UsuarioId { get; set; }

        [ValidateNever]
        public ApplicationUser? Usuario { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = "";

        [Required]
        [StringLength(150)]
        public string Email { get; set; } = "";

        // "Empresa", "Cliente" ou "Funcionário" — contexto de quem mandou.
        [StringLength(30)]
        public string Origem { get; set; } = "";

        [Required]
        [StringLength(150)]
        public string Assunto { get; set; } = "";

        [Required]
        [StringLength(3000)]
        public string Mensagem { get; set; } = "";

        [Required]
        public StatusAtendimento Status { get; set; } = StatusAtendimento.Aguardando;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public DateTime? DataFinalizacao { get; set; }
    }
}
