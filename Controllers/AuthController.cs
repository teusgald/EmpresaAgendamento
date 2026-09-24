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
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] ILogger<AuthController> logger)
        {
            // Precisa checar a role ANTES de deslogar — depois do SignOutAsync
            // o User já não carrega mais nenhuma claim/role.
            var ehCliente = User.IsInRole("Cliente");

            try
            {
                await signInManager.SignOutAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao encerrar sessão.");
            }

            // Funcionário também usa o modal de login da home (mesmo formulário
            // já atende Empresa e Funcionário) — não a tela dedicada /funcionario/login.
            return Redirect(ehCliente ? "/?login=true&type=cliente" : "/?login=true&type=empresa");
        }
    }
}
