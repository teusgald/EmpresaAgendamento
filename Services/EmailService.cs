using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EmpresaAgendamento.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            var host = _configuration["Smtp:Host"];
            var port = int.Parse(_configuration["Smtp:Port"] ?? "465");
            var remetente = _configuration["Smtp:User"];
            var senha = _configuration["Smtp:Password"];

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(remetente));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlMessage
            }.ToMessageBody();

            try
            {
                using var smtp = new SmtpClient();

                // Porta 465 = SSL implícito (SecureSocketOptions.SslOnConnect);
                // 587 usaria StartTls. Auto detecta pela porta configurada.
                await smtp.ConnectAsync(host, port, SecureSocketOptions.Auto);
                await smtp.AuthenticateAsync(remetente, senha);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Vários chamadores engolem essa exceção (não bloqueiam o
                // cadastro por causa do e-mail) — sem logar aqui, uma falha de
                // envio fica invisível pra sempre. Loga e relança, pra quem
                // chamou continuar decidindo o que fazer com o erro.
                _logger.LogError(
                    ex,
                    "Falha ao enviar e-mail via {Host}:{Port} (remetente {Remetente}, destinatário {Destinatario}, assunto '{Assunto}').",
                    host,
                    port,
                    remetente,
                    to,
                    subject);

                throw;
            }
        }
    }
}
