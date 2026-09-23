using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EmpresaAgendamento.Models
{
    // Produto consumido durante o atendimento (ex.: bebida, creme) — não é
    // vendido pelo sistema, só entra na comanda do agendamento e soma no
    // valor cobrado junto com o serviço. Aparece também na página pública,
    // numa aba ao lado de Serviços, só como catálogo/vitrine.
    public class Produto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = null!;

        [StringLength(1000)]
        public string? Descricao { get; set; }

        public decimal Preco { get; set; }

        public string? FotoUrl { get; set; }

        public int QuantidadeEstoque { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

        public int EmpresaId { get; set; }

        [ValidateNever]
        public Empresa Empresa { get; set; } = null!;

        public ICollection<ItemComanda> ItensComanda { get; set; }
            = new List<ItemComanda>();
    }
}
