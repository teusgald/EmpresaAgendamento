using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    [Route("funcionario")]
    public class FuncionarioAuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public FuncionarioAuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet("login")]
        public IActionResult Login() => View(new FuncionarioLoginViewModel());

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(FuncionarioLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var users = _userManager.Users.Where(u => u.Email == model.Email).ToList();

            ApplicationUser? user = null;

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Funcionario"))
                {
                    user = u;
                    break;
                }
            }

            if (user == null)
            {
                ModelState.AddModelError("", "Email ou senha inválidos.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, true);

            if (!result.Succeeded)
            {
                var erro = result.IsLockedOut
                    ? "Muitas tentativas de login. Tente novamente em alguns minutos."
                    : "Email ou senha inválidos.";

                ModelState.AddModelError("", erro);
                return View(model);
            }

            return RedirectToAction("Index", "Agendamentos");
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet("definir-senha")]
        public IActionResult DefinirSenha(string email, string token)
        {
            return View(new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            });
        }

        [HttpPost("definir-senha")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirSenha(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _userManager.Users.FirstOrDefault(x => x.Email == model.Email);

            if (user == null || !await _userManager.IsInRoleAsync(user, "Funcionario"))
            {
                ModelState.AddModelError("", "Link inválido ou expirado.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    "",
                    string.Join(" ", result.Errors.Select(x => x.Description)));

                return View(model);
            }

            ToastHelper.Success(TempData, "Senha definida com sucesso! Faça login para continuar.");
            return RedirectToAction(nameof(Login));
        }

        // Página de aviso pra quando a assinatura da empresa está inativa —
        // funcionário não mexe em pagamento, só o dono da empresa (ver
        // RequerAssinaturaAtivaFilter).
        [HttpGet("bloqueado")]
        public IActionResult Bloqueado() => View();
    }
}
