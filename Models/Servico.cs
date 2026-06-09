using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EmpresaAgendamento.Models
{
    public class Servico
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = null!;

        [StringLength(1000)]
        public string? Descricao { get; set; }

        public decimal Preco { get; set; }

        public DateTime DataCadastro { get; set; }
    = DateTime.UtcNow;
        [Range(1, 1440)]
        public int DuracaoMinutos { get; set; }
        [StringLength(20)]
        public string? CorAgenda { get; set; }
        public int OrdemExibicao { get; set; }
        public bool Ativo { get; set; } = true;

        // Multiempresa
        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        public ICollection<Agendamento> Agendamentos { get; set; }
     = new List<Agendamento>();

        public ICollection<FuncionarioServico> Funcionarios { get; set; }
            = new List<FuncionarioServico>();
    }
}