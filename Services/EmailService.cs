using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace EmpresaAgendamento.Services
{
    public class EmailService : IEmailService
    {
        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            var message = new MailMessage
            {
                From = new MailAddress("seuemail@gmail.com"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            message.To.Add(to);

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential("mateus.gald@gmail.com", "dvgs apnr avmm jpax"),
                EnableSsl = true
            };

            await smtp.SendMailAsync(message);
        }
    }
}
