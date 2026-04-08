using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EmpresaAgendamento.Models
{
    public class Servico
    {
        public int Id { get; set; }

        [Required]
        public string Nome { get; set; }

        public decimal Preco { get; set; }

        public int DuracaoMinutos { get; set; }

        // Multiempresa
        public int EmpresaId { get; set; }
        [ValidateNever]
        public Empresa Empresa { get; set; }

        public ICollection<Agendamento>? Agendamentos { get; set; }
    }
}
