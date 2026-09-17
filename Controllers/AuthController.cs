using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    [Route("")]
    public class AuthController : Controller
    {
        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(
            [FromServices] SignInManager<ApplicationUser> signInManager)
        {
            // Precisa checar a role ANTES de deslogar — depois do SignOutAsync
            // o User já não carrega mais nenhuma claim/role.
            var ehCliente = User.IsInRole("Cliente");
            var ehFuncionario = User.IsInRole("Funcionario");

            await signInManager.SignOutAsync();

            if (ehFuncionario)
            {
                return Redirect("/funcionario/login");
            }

            return Redirect(ehCliente ? "/?login=true&type=cliente" : "/?login=true&type=empresa");
        }
    }
}
