using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EmpresaAgendamento.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            var remetente = _configuration["Smtp:User"];
            var senha = _configuration["Smtp:Password"];

            var message = new MailMessage
            {
                From = new MailAddress(remetente),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            message.To.Add(to);

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(remetente, senha),
                EnableSsl = true
            };

            await smtp.SendMailAsync(message);
        }
    }
}
