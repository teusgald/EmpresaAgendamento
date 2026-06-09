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

        public EmpresaAuthController(
            IEmpresaService empresaService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService)
        {
            _empresaService = empresaService;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
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
        public async Task<IActionResult> Register(EmpresaRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    error = "Preencha todos os campos obrigatórios."
                });
            }

            var result = await _empresaService.RegisterAsync(model);

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
                error = result.Error
            });
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
                return Json(new
                {
                    success = false,
                    error = ex.Message
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
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }
}