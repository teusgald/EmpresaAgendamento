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

        // Marca se o lembrete automático (WhatsApp) já foi enviado pra esse
        // agendamento — evita mandar duplicado a cada ciclo do serviço em
        // segundo plano (ver LembreteAgendamentoBackgroundService).
        public bool LembreteEnviado { get; set; }

        // Se esse agendamento consumiu 1 crédito de um plano do cliente (ver
        // módulo Planos), guarda qual assinatura foi debitada — assim, se o
        // agendamento for cancelado, devolve o crédito certo (ver
        // IPlanoCreditoService.DevolverCreditoAsync).
        public int? AssinaturaPlanoServicoId { get; set; }
        public AssinaturaPlanoServico? AssinaturaPlanoServico { get; set; }

        // Evita contar a mesma visita duas vezes no programa de fidelidade
        // se o status for alterado para Finalizado mais de uma vez.
        public bool VisitaFidelidadeContabilizada { get; set; }

        // Produtos consumidos durante o atendimento (comanda) — somados ao
        // valor do serviço na mesma Conta a Receber.
        public ICollection<ItemComanda> ItensComanda { get; set; }
            = new List<ItemComanda>();

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