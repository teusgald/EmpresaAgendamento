using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    public class Empresa
    {
        public int Id { get; set; }

        [Required]
        public string Nome { get; set; }

        public string? Telefone { get; set; }
        public string? Email { get; set; }
        public string? Endereco { get; set; }

        [Required] // obrigatório
        [EmailAddress]
        public string EmailContato { get; set; }  // removido '?' → não nullable

        public string? HorarioFuncionamento { get; set; }
        public ICollection<ApplicationUser>? Usuarios { get; set; }
        public bool Ativo { get; set; } = true;

        // Relacionamentos
        public ICollection<Cliente>? Clientes { get; set; }
        public ICollection<Servico>? Servicos { get; set; }
        public ICollection<Agendamento>? Agendamentos { get; set; }
        public ICollection<EmpresaCliente>? EmpresaClientes { get; set; }
    }
}