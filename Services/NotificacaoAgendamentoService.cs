using EmpresaAgendamento.Data;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class NotificacaoAgendamentoService : INotificacaoAgendamentoService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificacaoAgendamentoService> _logger;

        public NotificacaoAgendamentoService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<NotificacaoAgendamentoService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task EnviarConfirmacaoAsync(int agendamentoId)
        {
            try
            {
                var agendamento = await _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.Empresa)
                    .FirstOrDefaultAsync(a => a.Id == agendamentoId);

                var email = agendamento?.Cliente?.Email;

                if (agendamento == null || string.IsNullOrWhiteSpace(email))
                    return;

                var nomeCliente = agendamento.Cliente!.Nome;
                var nomeEmpresa = agendamento.Empresa.NomeFantasia ?? agendamento.Empresa.Nome;

                await _emailService.SendEmailAsync(
                    email!,
                    $"Agendamento confirmado — {nomeEmpresa}",
                    $@"
                    <h2>Agendamento confirmado!</h2>
                    <p>Olá {nomeCliente}, seu horário está reservado:</p>
                    <p><strong>Empresa:</strong> {nomeEmpresa}</p>
                    <p><strong>Serviço:</strong> {agendamento.Servico.Nome}</p>
                    <p><strong>Data e hora:</strong> {agendamento.DataHora:dd/MM/yyyy 'às' HH:mm}</p>
                    <p>Se precisar cancelar ou reagendar, entre em contato direto com a empresa.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de confirmação (agendamento {AgendamentoId}).", agendamentoId);
            }
        }
    }
}
