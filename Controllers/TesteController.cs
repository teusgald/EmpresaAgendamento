using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    public class TesteController : Controller
    {
        private readonly IEmailService _emailService;

        public TesteController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task<IActionResult> EnviarEmailTeste()
        {
            await _emailService.SendEmailAsync(
                "mateus.gald@gmail.com",
                "Teste de Email",
                "<h1>Email funcionando 🚀</h1>"
            );

            return Content("Email enviado!");
        }
    }
}
