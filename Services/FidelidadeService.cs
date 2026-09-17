using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EmpresaAgendamento.Services
{
    public class FidelidadeService : IFidelidadeService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificacaoService _notificacaoService;
        private readonly ILogger<FidelidadeService> _logger;

        public FidelidadeService(ApplicationDbContext context, INotificacaoService notificacaoService, ILogger<FidelidadeService> logger)
        {
            _context = context;
            _notificacaoService = notificacaoService;
            _logger = logger;
        }

        public async Task RegistrarVisitaSeAplicavelAsync(int agendamentoId)
        {
            try
            {
                var agendamento = await _context.Agendamentos.FirstOrDefaultAsync(a => a.Id == agendamentoId);
                if (agendamento == null || agendamento.ClienteId == null || agendamento.VisitaFidelidadeContabilizada)
                    return;

                // Todo programa geral (ServicoId null) conta essa visita, e
                // também o programa específico desse serviço, se existir —
                // os dois progridem em paralelo, cada um com sua própria meta.
                var programas = await _context.ProgramasFidelidade
                    .Include(p => p.Servico)
                    .Where(p => p.EmpresaId == agendamento.EmpresaId && p.Ativo
                        && (p.ServicoId == null || p.ServicoId == agendamento.ServicoId))
                    .ToListAsync();

                if (programas.Count == 0)
                    return;

                var desbloqueios = new List<(ProgramaFidelidade Programa, int Quantidade)>();

                foreach (var programa in programas)
                {
                    var progresso = await _context.FidelidadeClientes
                        .FirstOrDefaultAsync(f => f.ProgramaFidelidadeId == programa.Id && f.ClienteId == agendamento.ClienteId.Value);

                    if (progresso == null)
                    {
                        progresso = new FidelidadeCliente
                        {
                            ProgramaFidelidadeId = programa.Id,
                            ClienteId = agendamento.ClienteId.Value
                        };
                        _context.FidelidadeClientes.Add(progresso);
                    }

                    progresso.VisitasContadas++;

                    var descontosDesbloqueados = 0;
                    while (progresso.VisitasContadas >= programa.VisitasNecessarias)
                    {
                        progresso.VisitasContadas -= programa.VisitasNecessarias;
                        progresso.DescontosDisponiveis++;
                        descontosDesbloqueados++;
                    }

                    progresso.DataUltimaAtualizacao = DateTime.UtcNow;

                    if (descontosDesbloqueados > 0)
                        desbloqueios.Add((programa, descontosDesbloqueados));
                }

                agendamento.VisitaFidelidadeContabilizada = true;

                await _context.SaveChangesAsync();

                foreach (var (programa, _) in desbloqueios)
                {
                    var descricaoDesconto = programa.TipoDesconto == TipoDescontoFidelidade.Percentual
                        ? $"{programa.ValorDesconto:0.##}%"
                        : programa.ValorDesconto.ToString("C");

                    var escopo = programa.Servico != null ? $" em {programa.Servico.Nome}" : "";

                    await _notificacaoService.NotificarClienteAsync(
                        agendamento.ClienteId.Value,
                        agendamento.EmpresaId,
                        TipoNotificacao.Fidelidade,
                        "Desconto de fidelidade liberado!",
                        $"Você atingiu {programa.VisitasNecessarias} visitas{escopo} e ganhou {descricaoDesconto} de desconto na próxima visita.",
                        "/Cliente");
                }
            }
            catch (Exception ex)
            {
                // Fidelidade é um efeito colateral da finalização do agendamento —
                // nunca pode impedir a conclusão do atendimento em si.
                _logger.LogError(ex, "Falha ao registrar visita de fidelidade (agendamento {AgendamentoId}).", agendamentoId);
            }
        }
    }
}
