using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    // O cliente "assinado" nesse plano — controle de crédito, sem cobrança
    // automática. DataInicioPeriodoAtual + CreditosUsados são recalculados
    // (reset) sempre que o período (semana/mês) já virou, na hora de checar
    // ou usar o crédito.
    public class AssinaturaPlanoServico
    {
        public int Id { get; set; }

        public int PlanoServicoId { get; set; }
        public PlanoServico PlanoServico { get; set; } = null!;

        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;

        public DateTime DataInicio { get; set; } = DateTime.UtcNow;

        public DateTime DataInicioPeriodoAtual { get; set; } = DateTime.UtcNow;

        public int CreditosUsados { get; set; }

        public StatusAssinaturaPlano Status { get; set; } = StatusAssinaturaPlano.Pendente;

        public DateTime? DataAprovacao { get; set; }

        public DateTime? DataCancelamento { get; set; }

        // Vencimento do período atual = quando o próximo período começa —
        // não é gravado no banco, é calculado a partir do início do período.
        public DateTime DataVencimento =>
            DataInicioPeriodoAtual.AddDays(
                PlanoServico.Periodicidade == PeriodicidadePlanoServico.Semanal ? 7 : 30);

        // Requer PlanoServico carregado (Include). Chamar sempre antes de
        // ler ou consumir CreditosUsados.
        public void AtualizarPeriodoSeNecessario()
        {
            if (Status != StatusAssinaturaPlano.Ativa)
                return;

            var duracaoPeriodo = PlanoServico.Periodicidade == PeriodicidadePlanoServico.Semanal
                ? TimeSpan.FromDays(7)
                : TimeSpan.FromDays(30);

            var agora = DateTime.UtcNow;

            while (agora - DataInicioPeriodoAtual >= duracaoPeriodo)
            {
                DataInicioPeriodoAtual += duracaoPeriodo;
                CreditosUsados = 0;
            }
        }

        public bool TemCreditoDisponivel()
        {
            if (Status != StatusAssinaturaPlano.Ativa)
                return false;

            AtualizarPeriodoSeNecessario();
            return CreditosUsados < PlanoServico.QuantidadeUsos;
        }
    }
}
