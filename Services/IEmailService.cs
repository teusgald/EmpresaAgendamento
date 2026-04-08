using System.Threading.Tasks;

namespace EmpresaAgendamento.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlMessage);
    }
}
