using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    public class ContaReceber
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Descricao { get; set; } = null!;

        public decimal ValorPrevisto { get; set; }

        public decimal ValorRecebido { get; set; }

        public FormaPagamento? FormaPagamento { get; set; }

        [Required]
        public DateTime DataVencimento { get; set; }

        public DateTime? DataRecebimento { get; set; }

        [Required]
        public StatusConta Status { get; set; } = StatusConta.Pendente;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public DateTime? DataCancelamento { get; set; }

        [StringLength(500)]
        public string? MotivoCancelamento { get; set; }

        [StringLength(500)]
        public string? Observacao { get; set; }

        // Identificador opaco pro link público do recibo (por e-mail) — nunca
        // expor o Id sequencial direto, senão dá pra adivinhar/listar recibo
        // de outras contas só incrementando o número na URL.
        public Guid ReciboToken { get; set; } = Guid.NewGuid();

        // =========================
        // 🔗 RELACIONAMENTOS
        // =========================

        // Multiempresa
        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        // Origem: um agendamento finalizado gera no máximo 1 conta a receber
        // (garantido também por índice único filtrado no banco).
        public int? AgendamentoId { get; set; }

        [ValidateNever]
        public Agendamento? Agendamento { get; set; }

        public int? ClienteId { get; set; }

        [ValidateNever]
        public Cliente? Cliente { get; set; }

        public int? CategoriaId { get; set; }

        [ValidateNever]
        public CategoriaFinanceira? Categoria { get; set; }

        public ICollection<MovimentacaoFinanceira> Movimentacoes { get; set; }
            = new List<MovimentacaoFinanceira>();
    }
}
