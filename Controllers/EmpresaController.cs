using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    [Route("empresa/inativo")]
    public class EmpresaController : Controller
    {
        [HttpGet("registro")]
        public IActionResult Registro()
        {
            return Redirect("/Identity/Account/Register");
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            return Redirect("/Identity/Account/Login");
        }
    }
}
