using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EmpresaAgendamento.Controllers
{
    // Login separado do portal de Empresa/Funcionario/Cliente — acesso
    // restrito ao dono do sistema (role "SuperAdmin", criada uma única vez
    // no seed do Program.cs).
    [Route("superadmin")]
    public class SuperAdminAuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<SuperAdminAuthController> _logger;

        public SuperAdminAuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<SuperAdminAuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login(bool sessaoExpirada = false)
        {
            if (sessaoExpirada)
            {
                ToastHelper.Warning(TempData, "Sua sessão expirou. Faça login novamente para continuar.");
            }

            return View(new SuperAdminLoginViewModel());
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(SuperAdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var users = _userManager.Users.Where(u => u.Email == model.Email).ToList();

                ApplicationUser? user = null;

                foreach (var u in users)
                {
                    if (await _userManager.IsInRoleAsync(u, "SuperAdmin"))
                    {
                        user = u;
                        break;
                    }
                }

                if (user == null)
                {
                    _logger.LogWarning("Login de SuperAdmin falhou (e-mail não encontrado): {Email}.", model.Email);
                    ModelState.AddModelError("", "Email ou senha inválidos.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(user, model.Password, true, true);

                if (!result.Succeeded)
                {
                    var erro = result.IsLockedOut
                        ? "Muitas tentativas de login. Tente novamente em alguns minutos."
                        : "Email ou senha inválidos.";

                    _logger.LogWarning("Login de SuperAdmin falhou para {Email}: {Motivo}.", model.Email, erro);
                    ModelState.AddModelError("", erro);
                    return View(model);
                }

                return RedirectToAction("Index", "SuperAdminDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar login de SuperAdmin para {Email}.", model.Email);
                ModelState.AddModelError("", "Erro ao processar login. Tente novamente.");
                return View(model);
            }
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
    }
}
