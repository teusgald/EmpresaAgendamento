using EmpresaAgendamento.Data;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class NotificacaoAgendamentoService : INotificacaoAgendamentoService
    {
        private const string TemplateConfirmacao = "confirmacao_agendamento";
        private const string IdiomaTemplate = "pt_BR";

        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<NotificacaoAgendamentoService> _logger;

        public NotificacaoAgendamentoService(
            ApplicationDbContext context,
            IEmailService emailService,
            IWhatsAppService whatsAppService,
            ILogger<NotificacaoAgendamentoService> logger)
        {
            _context = context;
            _emailService = emailService;
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        public async Task EnviarConfirmacaoAsync(int agendamentoId)
        {
            var agendamento = await _context.Agendamentos
                .Include(a => a.Cliente)
                .Include(a => a.Servico)
                .Include(a => a.Empresa).ThenInclude(e => e.Plano)
                .FirstOrDefaultAsync(a => a.Id == agendamentoId);

            if (agendamento == null)
                return;

            var nomeCliente = agendamento.Cliente?.Nome ?? agendamento.NomeClienteAvulso ?? "Cliente";
            var nomeEmpresa = agendamento.Empresa.NomeFantasia ?? agendamento.Empresa.Nome;

            var email = agendamento.Cliente?.Email;

            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
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

            if (agendamento.Empresa.Plano?.PermiteWhatsapp == true)
            {
                var telefone = agendamento.Cliente?.Telefone ?? agendamento.TelefoneClienteAvulso;

                if (!string.IsNullOrWhiteSpace(telefone))
                {
                    try
                    {
                        await _whatsAppService.EnviarTemplateAsync(
                            telefone,
                            agendamento.Empresa.WhatsAppPhoneNumberId,
                            TemplateConfirmacao,
                            IdiomaTemplate,
                            new[]
                            {
                                nomeCliente,
                                nomeEmpresa,
                                agendamento.Servico.Nome,
                                agendamento.DataHora.ToString("dd/MM"),
                                agendamento.DataHora.ToString("HH:mm")
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falha ao enviar confirmação por WhatsApp (agendamento {AgendamentoId}).", agendamentoId);
                    }
                }
            }
        }
    }
}
