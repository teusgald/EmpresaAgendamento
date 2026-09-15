using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    // Extrato/"Caixa": cada linha é um lançamento confirmado (ou o estorno de um
    // lançamento anterior). Nunca é excluída — cancelamento gera uma nova linha
    // de sinal contrário referenciando a original.
    public class MovimentacaoFinanceira
    {
        public int Id { get; set; }

        [Required]
        public TipoMovimentacao Tipo { get; set; }

        [Required]
        public OrigemMovimentacao Origem { get; set; }

        public decimal Valor { get; set; }

        public FormaPagamento? FormaPagamento { get; set; }

        public DateTime DataMovimento { get; set; } = DateTime.UtcNow;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Required]
        public StatusMovimentacao Status { get; set; } = StatusMovimentacao.Confirmada;

        [StringLength(300)]
        public string? Descricao { get; set; }

        // Quando esta linha é o estorno de outra, aponta para a original.
        public int? MovimentacaoOrigemEstornoId { get; set; }

        [ValidateNever]
        public MovimentacaoFinanceira? MovimentacaoOrigemEstorno { get; set; }

        // =========================
        // 🔗 RELACIONAMENTOS
        // =========================

        // Multiempresa
        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        public int? ContaReceberId { get; set; }

        [ValidateNever]
        public ContaReceber? ContaReceber { get; set; }

        public int? ContaPagarId { get; set; }

        [ValidateNever]
        public ContaPagar? ContaPagar { get; set; }

        public int? CategoriaId { get; set; }

        [ValidateNever]
        public CategoriaFinanceira? Categoria { get; set; }

        // Usuário (login da Empresa) que registrou o lançamento
        public string? UsuarioId { get; set; }

        [ValidateNever]
        public ApplicationUser? Usuario { get; set; }
    }
}
