using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

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

        [Required]
        public int ClienteId { get; set; }

        [Required]
        public int ServicoId { get; set; }

        // Multiempresa
        [Required] // 🔥 importante para SaaS
        public int EmpresaId { get; set; }

        // =========================
        // 📅 CONTROLE
        // =========================

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public bool Ativo { get; set; } = true;

        // =========================
        // 🔁 NAVIGATION
        // =========================

        [ValidateNever]
        public Cliente Cliente { get; set; }

        [ValidateNever]
        public Servico Servico { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; }
    }
}