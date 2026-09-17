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
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
