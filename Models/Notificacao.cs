using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    // Notificação da empresa (visível pro dono e pros funcionários) sobre
    // eventos do dia a dia — agendamento, pagamento, cadastro etc.
    public class Notificacao
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        // Preenchido só quando a notificação é pro cliente (não pra empresa/
        // funcionário) — ex.: "seu agendamento foi confirmado".
        public int? ClienteId { get; set; }

        [ValidateNever]
        public Cliente? Cliente { get; set; }

        [Required]
        public TipoNotificacao Tipo { get; set; }

        [Required]
        [StringLength(150)]
        public string Titulo { get; set; } = "";

        [Required]
        [StringLength(500)]
        public string Mensagem { get; set; } = "";

        // Pra onde o clique na notificação leva (opcional).
        [StringLength(300)]
        public string? Link { get; set; }

        public bool Lida { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public DateTime? DataLeitura { get; set; }
    }
}
