using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class PlanoCreditoService : IPlanoCreditoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlanoCreditoService> _logger;

        public PlanoCreditoService(ApplicationDbContext context, ILogger<PlanoCreditoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int?> ConsumirSeAplicavelAsync(int empresaId, int clienteId, int servicoId)
        {
            try
            {
                var assinatura = await _context.AssinaturasPlanoServico
                    .Include(a => a.PlanoServico)
                    .Where(a =>
                        a.Status == StatusAssinaturaPlano.Ativa &&
                        a.ClienteId == clienteId &&
                        a.PlanoServico.EmpresaId == empresaId &&
                        a.PlanoServico.Servicos.Any(x => x.ServicoId == servicoId))
                    .FirstOrDefaultAsync();

                if (assinatura == null || !assinatura.TemCreditoDisponivel())
                    return null;

                assinatura.CreditosUsados++;
                await _context.SaveChangesAsync();

                return assinatura.Id;
            }
            catch (Exception ex)
            {
                // Não bloqueia o agendamento por um problema aqui — pior caso,
                // o cliente não usa o crédito do plano dessa vez.
                _logger.LogError(
                    ex,
                    "Falha ao consumir crédito de plano (empresa {EmpresaId}, cliente {ClienteId}, serviço {ServicoId}).",
                    empresaId, clienteId, servicoId);

                return null;
            }
        }

        public async Task DevolverCreditoAsync(int assinaturaPlanoServicoId)
        {
            try
            {
                var assinatura = await _context.AssinaturasPlanoServico
                    .FirstOrDefaultAsync(a => a.Id == assinaturaPlanoServicoId);

                if (assinatura == null)
                    return;

                if (assinatura.CreditosUsados > 0)
                {
                    assinatura.CreditosUsados--;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao devolver crédito de plano (assinatura {AssinaturaId}).", assinaturaPlanoServicoId);
            }
        }
    }
}
