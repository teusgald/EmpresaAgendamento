using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    [Route("empresa")]
    public class EmpresaAuthController : Controller
    {
        private readonly IEmpresaService _empresaService;

        public EmpresaAuthController(IEmpresaService empresaService)
        {
            _empresaService = empresaService;
        }

        [HttpGet("login")]
        public IActionResult Login() => View();

        [HttpPost("login")]
        public async Task<IActionResult> Login(EmpresaLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, error = "Dados inválidos" });

            var result = await _empresaService.LoginAsync(model);

            if (result.Success)
                return Json(new
                {
                    success = true,
                    redirect = "/Empresa/Dashboard"
                });

            return Json(new { success = false, error = "Email ou senha inválidos" });
        }



        [HttpGet("registro")]
        public IActionResult Register() => View();

        [HttpPost("registro")]
        public async Task<IActionResult> Register(EmpresaRegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _empresaService.RegisterAsync(model);

            if (result.Success)
                return Json(new
                {
                    success = true,
                    redirect = "/Empresas/Dashboard"
                });

            ModelState.AddModelError("", result.Error);
            return View(model);
        }

        
        public async Task<IActionResult> Logout([FromServices] SignInManager<ApplicationUser> signInManager)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
