using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    [Route("empresa")]
    public class EmpresaAuthController : Controller
    {
        private readonly IEmpresaService _empresaService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmpresaAuthController> _logger;

        public EmpresaAuthController(
            IEmpresaService empresaService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            ILogger<EmpresaAuthController> logger)
        {
            _empresaService = empresaService;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login() => View();

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(EmpresaLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, error = "Dados inválidos" });

            var result = await _empresaService.LoginAsync(model);

            if (result.Success)
            {
                return Json(new
                {
                    success = true,
                    redirect = "/Empresa/Dashboard"
                });
            }

            return Json(new
            {
                success = false,
                error = "Email ou senha inválidos"
            });
        }

        [HttpGet("registro")]
        public IActionResult Register() => View();

        [HttpPost("registro")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            EmpresaRegisterViewModel model, bool aceitaTermos = false, string? tipoPlanoEscolhido = null)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    error = "Preencha todos os campos obrigatórios."
                });
            }

            if (!aceitaTermos)
            {
                return Json(new
                {
                    success = false,
                    error = "É necessário aceitar os Termos de Uso e a Política de Privacidade."
                });
            }

            var result = await _empresaService.RegisterAsync(model, tipoPlanoEscolhido);

            if (!result.Success)
            {
                return Json(new
                {
                    success = false,
                    error = result.Error
                });
            }

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(result.User!);

                var link =
                    $"{Request.Scheme}://{Request.Host}/empresa/confirmar-email" +
                    $"?userId={Uri.EscapeDataString(result.User!.Id)}" +
                    $"&token={Uri.EscapeDataString(token)}";

                await _emailService.SendEmailAsync(
                    model.Email,
                    "Confirme seu e-mail — Simpli Time",
                    $@"
                    <h2>Bem-vindo ao Simpli Time!</h2>
                    <p>Falta pouco — confirme seu e-mail para ativar sua conta:</p>
                    <p><a href='{link}'>Confirmar e-mail</a></p>
                    <p>Se você não fez esse cadastro, ignore este e-mail.</p>"
                );
            }
            catch
            {
                // Não falha o cadastro por causa do e-mail — a empresa pode
                // pedir reenvio depois; a conta já foi criada normalmente.
            }

            return Json(new
            {
                success = true,
                message = "Cadastro realizado! Verifique seu e-mail para confirmar a conta antes de entrar."
            });
        }

        [HttpGet("confirmar-email")]
        public async Task<IActionResult> ConfirmarEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return Redirect("/");
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return Redirect("/");
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);

            // A home page já sabe abrir o modal de login via esses parâmetros
            // (mesmo padrão usado no link de reset de senha).
            return Redirect(result.Succeeded
                ? "/?login=true&type=empresa&confirmado=true"
                : "/?login=true&type=empresa&confirmado=false");
        }

     

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ======================================
        // RECUPERAR SENHA
        // ======================================

        [HttpGet("forgot")]
        public IActionResult Forgot()
        {
            return View();
        }

        [HttpPost("forgot")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Forgot(ForgotViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Email inválido"
                    });
                }

                var user = _userManager.Users
                            .FirstOrDefault(x =>
                                x.Email == model.Email &&
                                x.EmpresaId != null);

                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Email não encontrado"
                    });
                }

                var token =
                    await _userManager.GeneratePasswordResetTokenAsync(user);

                var link =
                           $"{Request.Scheme}://{Request.Host}/" +
                           $"?mode=reset" +
                           $"&type=empresa" +
                           $"&email={Uri.EscapeDataString(model.Email)}" +
                           $"&token={Uri.EscapeDataString(token)}";

                await _emailService.SendEmailAsync(
                    model.Email,
                    "Recuperação de Senha",
                    $@"
                    <h2>Recuperação de Senha</h2>

                    <p>Recebemos uma solicitação para redefinir sua senha.</p>

                    <p>
                        <a href='{link}'>
                            Clique aqui para redefinir sua senha
                        </a>
                    </p>

                    <p>Se você não solicitou essa alteração, ignore este email.</p>"
                );

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar recuperação de senha (empresa, e-mail {Email}).", model.Email);

                return Json(new
                {
                    success = false,
                    error = "Não foi possível enviar o e-mail de recuperação agora. Tente novamente em alguns instantes."
                });
            }
        }

        [HttpGet("resetpassword")]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            });
        }

        [HttpPost("resetpassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Dados inválidos"
                    });
                }

                var user = _userManager.Users
                            .FirstOrDefault(x =>
                                x.Email == model.Email &&
                                x.EmpresaId.HasValue);

                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Usuário não encontrado"
                    });
                }

                var result = await _userManager.ResetPasswordAsync(
                                     user,
                                     model.Token,
                                     model.Password);

                if (result.Succeeded)
                {

                    return Json(new
                    {
                        success = true,
                        redirect = "/?login=true&type=empresa&reset=success"
                    });
                }

                var erros = result.Errors
                    .Select(x => $"{x.Code} - {x.Description}")
                    .ToList();

                return Json(new
                {
                    success = false,
                    error = string.Join("<br>", erros)
                });


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao redefinir senha (empresa, e-mail {Email}).", model.Email);

                return Json(new
                {
                    success = false,
                    error = "Não foi possível redefinir sua senha agora. Tente novamente em alguns instantes."
                });
            }
        }
    }
}