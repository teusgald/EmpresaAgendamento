using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Models
{
    public class Agendamento
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "A data e hora são obrigatórias")]
        public DateTime DataHora { get; set; }

        [Required]
        public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

        [MaxLength(500)]
        public string? Observacao { get; set; }

        // =========================
        // 🔗 RELACIONAMENTOS
        // =========================

        public int? ClienteId { get; set; }

        [Required]
        public int ServicoId { get; set; }

        // Multiempresa
        [Required] // 🔥 importante para SaaS
        public int EmpresaId { get; set; }

        // =========================
        // 📅 CONTROLE
        // =========================

        public DateTime DataCriacao { get; set; }
    = DateTime.UtcNow;
        public DateTime? DataAtualizacao { get; set; }
        public DateTime? DataCancelamento { get; set; }
        [MaxLength(500)]
        public string? MotivoCancelamento { get; set; }
        public bool Ativo { get; set; } = true;

        public int? FuncionarioId { get; set; }
        public Funcionario? Funcionario { get; set; }

        public string? NomeClienteAvulso { get; set; }
        public string? TelefoneClienteAvulso { get; set; }

        public bool ClienteAvulso { get; set; }

        // =========================
        // 🔁 NAVIGATION
        // =========================

        [ValidateNever]
        public Cliente? Cliente { get; set; }

        [ValidateNever]
        public Servico Servico { get; set; } = null!;

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        public ICollection<AgendamentoFuncionario> Funcionarios { get; set; }
    = new List<AgendamentoFuncionario>();
    }
}