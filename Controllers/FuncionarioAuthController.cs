using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EmpresaAgendamento.Controllers
{
    [Route("funcionario")]
    public class FuncionarioAuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FuncionarioAuthController> _logger;

        public FuncionarioAuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<FuncionarioAuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login(bool sessaoExpirada = false)
        {
            if (sessaoExpirada)
            {
                ToastHelper.Warning(TempData, "Sua sessão expirou. Faça login novamente para continuar.");
            }

            return View(new FuncionarioLoginViewModel());
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
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
                _logger.LogWarning("Login de funcionário falhou (e-mail não encontrado): {Email}.", model.Email);
                ModelState.AddModelError("", "Email ou senha inválidos.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, true);

            if (!result.Succeeded)
            {
                var erro = result.IsLockedOut
                    ? "Muitas tentativas de login. Tente novamente em alguns minutos."
                    : "Email ou senha inválidos.";

                _logger.LogWarning("Login de funcionário falhou para {Email}: {Motivo}.", model.Email, erro);
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

        // ======================================
        // RECUPERAR SENHA
        // ======================================

        [HttpGet("forgot")]
        public IActionResult Forgot() => View(new ForgotViewModel());

        [HttpPost("forgot")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Forgot(ForgotViewModel model)
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
                    if (await _userManager.IsInRoleAsync(u, "Funcionario"))
                    {
                        user = u;
                        break;
                    }
                }

                // Resposta sempre igual, exista ou não a conta — senão dá pra
                // descobrir quais e-mails têm acesso de funcionário só testando
                // esse formulário.
                if (user != null)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                    var link =
                        $"{LinkBaseHelper.ObterBase(Request, _configuration)}/funcionario/definir-senha" +
                        $"?email={Uri.EscapeDataString(user.Email!)}" +
                        $"&token={Uri.EscapeDataString(token)}";

                    await _emailService.SendEmailAsync(
                        user.Email!,
                        "Recuperação de Senha — Simpli Time",
                        $@"
                        <h2>Recuperação de Senha</h2>
                        <p>Recebemos uma solicitação para redefinir sua senha de acesso.</p>
                        <p><a href='{link}'>Clique aqui para definir uma nova senha</a></p>
                        <p>Se você não solicitou essa alteração, ignore este e-mail.</p>");
                }
                else
                {
                    _logger.LogInformation("Recuperação de senha solicitada para e-mail de funcionário não cadastrado: {Email}.", model.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar recuperação de senha (funcionário, e-mail {Email}).", model.Email);
            }

            ToastHelper.Success(TempData, "Se esse e-mail estiver cadastrado, enviamos um link de recuperação para ele.");
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
        [EnableRateLimiting("auth")]
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
