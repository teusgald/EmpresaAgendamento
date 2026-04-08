using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;


namespace EmpresaAgendamento.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required]
        public string Nome { get; set; }

        public string? Telefone { get; set; }
        public string? Email { get; set; }

        // vínculo com identity
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public ICollection<Agendamento>? Agendamentos { get; set; }

        public ICollection<EmpresaCliente>? EmpresaClientes { get; set; }
    }
}
