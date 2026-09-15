using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    public class ContaPagar
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Descricao { get; set; } = null!;

        public decimal ValorPrevisto { get; set; }

        public decimal ValorPago { get; set; }

        public FormaPagamento? FormaPagamento { get; set; }

        [Required]
        public DateTime DataVencimento { get; set; }

        public DateTime? DataPagamento { get; set; }

        [Required]
        public StatusConta Status { get; set; } = StatusConta.Pendente;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public DateTime? DataCancelamento { get; set; }

        [StringLength(500)]
        public string? MotivoCancelamento { get; set; }

        [StringLength(500)]
        public string? Observacao { get; set; }

        // =========================
        // 🔗 RELACIONAMENTOS
        // =========================

        // Multiempresa
        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        // Preenchido quando a conta a pagar é um pagamento de comissão
        public int? FuncionarioId { get; set; }

        [ValidateNever]
        public Funcionario? Funcionario { get; set; }

        public int? CategoriaId { get; set; }

        [ValidateNever]
        public CategoriaFinanceira? Categoria { get; set; }

        public ICollection<MovimentacaoFinanceira> Movimentacoes { get; set; }
            = new List<MovimentacaoFinanceira>();

        // Comissões (AgendamentoFuncionario) quitadas por este pagamento
        public ICollection<AgendamentoFuncionario> Comissoes { get; set; }
            = new List<AgendamentoFuncionario>();
    }
}
