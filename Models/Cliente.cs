using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;


namespace EmpresaAgendamento.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = null!;

        [StringLength(25)]
        public string? Telefone { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? Email { get; set; }
        public bool Ativo { get; set; } = true;
        public DateTime DataCadastro { get; set; }
    = DateTime.UtcNow;

        // vínculo com identity
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public ICollection<Agendamento> Agendamentos { get; set; }
     = new List<Agendamento>();
        public ICollection<EmpresaCliente> EmpresaClientes { get; set; }
    = new List<EmpresaCliente>();
    }
}
