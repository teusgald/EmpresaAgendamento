using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EmpresaAgendamento.Models
{
    // Um produto consumido dentro da comanda de um agendamento — a comanda
    // em si não é uma entidade própria, é só "os ItemComanda desse
    // agendamento". PrecoUnitario é uma cópia do preço do Produto no
    // momento em que foi adicionado (não muda se o preço do produto mudar
    // depois, igual já funciona no Servico.Preco snapshotado na ContaReceber).
    public class ItemComanda
    {
        public int Id { get; set; }

        public int AgendamentoId { get; set; }

        [ValidateNever]
        public Agendamento Agendamento { get; set; } = null!;

        public int ProdutoId { get; set; }

        [ValidateNever]
        public Produto Produto { get; set; } = null!;

        public int Quantidade { get; set; }

        public decimal PrecoUnitario { get; set; }

        public DateTime DataAdicao { get; set; } = DateTime.UtcNow;
    }
}
