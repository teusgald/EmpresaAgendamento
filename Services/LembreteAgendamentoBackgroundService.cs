using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    // Roda em segundo plano durante toda a vida da aplicação: a cada 15
    // minutos, varre os agendamentos que caem entre 23h e 25h a partir de
    // agora (ou seja, cerca de 1 dia antes) e manda um lembrete por WhatsApp
    // pra reduzir falta — só pra empresas cujo plano permite (Plano.PermiteWhatsapp).
    public class LembreteAgendamentoBackgroundService : BackgroundService
    {
        private static readonly TimeSpan IntervaloVarredura = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LembreteAgendamentoBackgroundService> _logger;

        public LembreteAgendamentoBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LembreteAgendamentoBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnviarLembretesPendentesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar lembretes de agendamento.");
                }

                try
                {
                    await Task.Delay(IntervaloVarredura, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // aplicação está parando — sai do loop normalmente.
                }
            }
        }

        private async Task EnviarLembretesPendentesAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            var agora = DateTime.Now;
            var janelaInicio = agora.AddHours(23);
            var janelaFim = agora.AddHours(25);

            var agendamentos = await context.Agendamentos
                .Include(a => a.Cliente)
                .Include(a => a.Servico)
                .Include(a => a.Empresa).ThenInclude(e => e.Plano)
                .Where(a =>
                    a.Ativo &&
                    !a.LembreteEnviado &&
                    a.Status != StatusAgendamento.Cancelado &&
                    a.DataHora >= janelaInicio &&
                    a.DataHora <= janelaFim &&
                    a.Empresa.Plano != null &&
                    a.Empresa.Plano.PermiteWhatsapp)
                .ToListAsync(stoppingToken);

            foreach (var agendamento in agendamentos)
            {
                var telefone = agendamento.Cliente?.Telefone ?? agendamento.TelefoneClienteAvulso;

                if (string.IsNullOrWhiteSpace(telefone))
                {
                    // Sem telefone não tem como avisar — marca como tratado
                    // pra não ficar reprocessando esse agendamento pra sempre.
                    agendamento.LembreteEnviado = true;
                    continue;
                }

                var nomeCliente = agendamento.Cliente?.Nome ?? agendamento.NomeClienteAvulso ?? "Cliente";

                var mensagem =
                    $"Olá {nomeCliente}! Passando pra lembrar do seu horário na " +
                    $"{agendamento.Empresa.Nome}: {agendamento.Servico.Nome} em " +
                    $"{agendamento.DataHora:dd/MM} às {agendamento.DataHora:HH:mm}. Até lá!";

                try
                {
                    await whatsApp.EnviarMensagemAsync(telefone, mensagem);
                    agendamento.LembreteEnviado = true;
                }
                catch (Exception ex)
                {
                    // Não marca como enviado — tenta de novo no próximo ciclo.
                    _logger.LogWarning(
                        ex,
                        "Falha ao enviar lembrete do agendamento {AgendamentoId}.",
                        agendamento.Id);
                }
            }

            if (agendamentos.Count > 0)
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
