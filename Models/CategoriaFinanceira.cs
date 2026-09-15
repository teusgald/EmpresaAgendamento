using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    public class CategoriaFinanceira
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; } = null!;

        [Required]
        public TipoCategoriaFinanceira Tipo { get; set; }

        public bool Ativo { get; set; } = true;

        public bool Padrao { get; set; }

        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

        // Multiempresa
        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;
    }
}
