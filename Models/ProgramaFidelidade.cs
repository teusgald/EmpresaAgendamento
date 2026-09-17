using System.ComponentModel.DataAnnotations;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    // Configuração de um programa de fidelidade da empresa — a cada X
    // visitas finalizadas, o cliente ganha automaticamente um desconto pra
    // usar na próxima. Sem cobrança/desconto automático no pagamento — a
    // empresa aplica manualmente quando o cliente for pagar, igual já
    // funciona no controle de crédito dos Planos. Uma empresa pode ter
    // vários programas (ex.: um geral + um específico de um serviço).
    public class ProgramaFidelidade
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        // Null = conta visita de qualquer serviço. Preenchido = só conta
        // visita desse serviço específico (ex.: "a cada 10 cortes de
        // cabelo").
        public int? ServicoId { get; set; }
        public Servico? Servico { get; set; }

        public bool Ativo { get; set; }

        [Range(1, 100)]
        public int VisitasNecessarias { get; set; } = 5;

        public TipoDescontoFidelidade TipoDesconto { get; set; } = TipoDescontoFidelidade.Percentual;

        [Range(0.01, 100000)]
        public decimal ValorDesconto { get; set; } = 10;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    }
}
