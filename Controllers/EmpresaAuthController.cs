using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmpresaAuthController> _logger;

        public EmpresaAuthController(
            IEmpresaService empresaService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<EmpresaAuthController> logger)
        {
            _empresaService = empresaService;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login() => View();

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(EmpresaLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, error = "Dados inválidos" });

            try
            {
                var result = await _empresaService.LoginAsync(model);

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        redirect = "/Empresa/Dashboard"
                    });
                }

                // Esse formulário agora atende empresa e funcionário — se não
                // achou como Empresa, tenta como Funcionário antes de desistir
                // (mesmo padrão de login usado em FuncionarioAuthController).
                if (await TentarLoginFuncionarioAsync(model.Email, model.Password))
                {
                    return Json(new
                    {
                        success = true,
                        redirect = "/Agendamentos/Index"
                    });
                }

                _logger.LogWarning("Login de empresa/funcionário falhou para o e-mail {Email}.", model.Email);

                return Json(new
                {
                    success = false,
                    error = "Email ou senha inválidos"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar login de empresa/funcionário para o e-mail {Email}.", model.Email);

                return Json(new
                {
                    success = false,
                    error = "Não foi possível entrar agora. Tente novamente em alguns instantes."
                });
            }
        }

        private async Task<bool> TentarLoginFuncionarioAsync(string email, string password)
        {
            var users = _userManager.Users.Where(u => u.Email == email).ToList();

            ApplicationUser? funcionario = null;

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Funcionario"))
                {
                    funcionario = u;
                    break;
                }
            }

            if (funcionario == null)
                return false;

            // isPersistent: true — ver o mesmo ajuste em EmpresaService.LoginAsync.
            var result = await _signInManager.PasswordSignInAsync(funcionario, password, true, true);

            return result.Succeeded;
        }

        [HttpGet("registro")]
        public IActionResult Register() => View();

        [HttpPost("registro")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(
            EmpresaRegisterViewModel model, bool aceitaTermos = false,
            string? tipoPlanoEscolhido = null, string? nomePlanoEscolhido = null)
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

            try
            {
                var result = await _empresaService.RegisterAsync(model, tipoPlanoEscolhido, nomePlanoEscolhido);

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
                        $"{LinkBaseHelper.ObterBase(Request, _configuration)}/empresa/confirmar-email" +
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
                catch (Exception ex)
                {
                    // Não falha o cadastro por causa do e-mail — a empresa pode
                    // pedir reenvio depois; a conta já foi criada normalmente.
                    _logger.LogError(ex, "Falha ao enviar e-mail de confirmação de cadastro pra empresa {Email}.", model.Email);
                }

                return Json(new
                {
                    success = true,
                    message = "Cadastro realizado! Verifique seu e-mail para confirmar a conta antes de entrar."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar cadastro de empresa para o e-mail {Email}.", model.Email);

                return Json(new
                {
                    success = false,
                    error = "Não foi possível concluir o cadastro agora. Tente novamente em alguns instantes."
                });
            }
        }

        [HttpGet("confirmar-email")]
        public async Task<IActionResult> ConfirmarEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return Redirect("/");
            }

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao confirmar e-mail de empresa (userId {UserId}).", userId);
                return Redirect("/?login=true&type=empresa&confirmado=false");
            }
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao encerrar sessão de empresa.");
            }

            return Redirect("/?login=true&type=empresa");
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
        [EnableRateLimiting("auth")]
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

                // Resposta sempre igual, exista ou não a conta — senão dá pra
                // descobrir quais e-mails têm cadastro só testando esse formulário.
                if (user != null)
                {
                    var token =
                        await _userManager.GeneratePasswordResetTokenAsync(user);

                    var link =
                               $"{LinkBaseHelper.ObterBase(Request, _configuration)}/" +
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
                }
                else
                {
                    _logger.LogInformation("Recuperação de senha solicitada para e-mail de empresa não cadastrado: {Email}.", model.Email);
                }

                return Json(new
                {
                    success = true,
                    message = "Se esse e-mail estiver cadastrado, enviamos um link de recuperação para ele."
                });
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
        [EnableRateLimiting("auth")]
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

                // Mensagem genérica tanto pra "e-mail não encontrado" quanto pra
                // "token inválido/expirado" — do contrário, dava pra descobrir
                // se um e-mail tem conta de empresa só testando esse formulário.
                const string erroGenerico = "Não foi possível redefinir a senha. O link pode ter expirado — solicite um novo.";

                var user = _userManager.Users
                            .FirstOrDefault(x =>
                                x.Email == model.Email &&
                                x.EmpresaId.HasValue);

                if (user == null)
                {
                    _logger.LogWarning("Tentativa de redefinir senha de empresa com e-mail não cadastrado: {Email}.", model.Email);

                    return Json(new { success = false, error = erroGenerico });
                }

                var result = await _userManager.ResetPasswordAsync(
                                     user,
                                     model.Token,
                                     model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Senha redefinida com sucesso (empresa, e-mail {Email}).", model.Email);

                    return Json(new
                    {
                        success = true,
                        redirect = "/?login=true&type=empresa&reset=success"
                    });
                }

                _logger.LogWarning(
                    "Falha ao redefinir senha de empresa para {Email}: {Erros}.",
                    model.Email,
                    string.Join("; ", result.Errors.Select(x => x.Code)));

                // Token inválido/expirado é o mesmo caso de "e-mail não existe"
                // (mensagem genérica); erro de política de senha (curta, sem
                // maiúscula etc.) é feedback legítimo — não vaza nada sobre a conta.
                var tokenInvalido = result.Errors.Any(e => e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase));

                return Json(new
                {
                    success = false,
                    error = tokenInvalido
                        ? erroGenerico
                        : string.Join("<br>", result.Errors.Select(x => x.Description))
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