using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("cliente")]
public class ClientesAuthController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;

    public ClientesAuthController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
    }

    // =========================
    // LOGIN
    // =========================
    [HttpGet("login")]
    public IActionResult Login() => View();

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(ClienteLoginViewModel model)
    {
        var origem = Request.Form["Origem"].ToString();

        if (!ModelState.IsValid)
        {
            if (origem == "publico")
            {
                return Json(new
                {
                    success = false,
                    error = "Dados inválidos."
                });
            }

            return View(model);
        }

        var users = await _userManager.Users
            .Where(u => u.Email == model.Email)
            .ToListAsync();

        ApplicationUser? user = null;

        foreach (var u in users)
        {
            if (await _userManager.IsInRoleAsync(u, "Cliente"))
            {
                user = u;
                break;
            }
        }

        if (user == null)
        {
            if (origem == "publico")
            {
                return Json(new
                {
                    success = false,
                    error = "Usuário não encontrado."
                });
            }

            ModelState.AddModelError("", "Usuário não encontrado.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            false,
            true);

        if (!result.Succeeded)
        {
            var mensagemErro = result.IsLockedOut
                ? "Muitas tentativas de login. Tente novamente em alguns minutos."
                : result.IsNotAllowed
                    ? "Confirme seu e-mail antes de entrar. Verifique sua caixa de entrada."
                    : "Email ou senha inválidos.";

            if (origem == "publico")
            {
                return Json(new
                {
                    success = false,
                    error = mensagemErro
                });
            }

            ModelState.AddModelError("", mensagemErro);
            return View(model);
        }

        // LOGIN VIA MODAL PÚBLICO
        if (origem == "publico")
        {
            return Json(new
            {
                success = true,
                redirect = "/Cliente/Agendamentos"
            });
        }

        // LOGIN NORMAL HOME 
        return RedirectToAction(
            "Agendamentos",
            "Cliente");
    }

    // =========================
    // REGISTER
    // =========================
    [HttpGet("registro")]
    public IActionResult Register() => View();

    [HttpPost("registro")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ClienteRegisterViewModel model, bool aceitaTermos = false)
    {
        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                error = "Preencha todos os campos corretamente."
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

        // =====================================
        // VALIDAR EMAIL DUPLICADO
        // =====================================

        var emailExistente = _userManager.Users
            .FirstOrDefault(x => x.Email == model.Email &&
                    x.Cliente.Id != null);

        if (emailExistente != null)
        {
            return Json(new
            {
                success = false,
                error = "Já existe uma conta cadastrada com este e-mail."
            });
        }

        // =====================================
        // CRIAR USUÁRIO
        // =====================================

        var user = new ApplicationUser
        {
            UserName = $"cliente-{Guid.NewGuid()}",
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(
            user,
            model.Password);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                error = string.Join("<br>",
                    result.Errors.Select(x => x.Description))
            });
        }

        // =====================================
        // ROLE
        // =====================================

        await _userManager.AddToRoleAsync(user, "Cliente");

        // =====================================
        // CLIENTE
        // =====================================

        var cliente = new Cliente
        {
            Nome = model.Nome,
            Email = model.Email,
            Telefone = model.Telefone,
            UserId = user.Id
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        user.ClienteId = cliente.Id;

        await _userManager.UpdateAsync(user);

        // =====================================
        // EMAIL DE CONFIRMAÇÃO
        // =====================================
        // Sem login automático mais — precisa confirmar o e-mail antes de entrar.

        bool emailEnviado = false;
        string erroEmail = "";

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var link =
                $"{Request.Scheme}://{Request.Host}/cliente/confirmar-email" +
                $"?userId={Uri.EscapeDataString(user.Id)}" +
                $"&token={Uri.EscapeDataString(token)}";

            await _emailService.SendEmailAsync(
                user.Email,
                "Confirme seu e-mail",
                $@"
            <h2>Olá {cliente.Nome}</h2>

            <p>Sua conta foi criada com sucesso. Falta só confirmar seu e-mail:</p>

            <p><a href='{link}'>Confirmar e-mail</a></p>

            <p>Se você não fez esse cadastro, ignore este e-mail.</p>
            ");

            emailEnviado = true;
        }
        catch (Exception ex)
        {
            erroEmail = ex.Message;
        }

        return Json(new
        {
            success = true,
            message = "Cadastro realizado! Verifique seu e-mail para confirmar a conta antes de entrar.",
            emailEnviado,
            erroEmail
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

        return Redirect(result.Succeeded
            ? "/?login=true&type=cliente&confirmado=true"
            : "/?login=true&type=cliente&confirmado=false");
    }

    // =========================
    // LOGOUT
    // =========================
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromServices] SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("forgot")]
    public IActionResult Forgot() => View();

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
                    x.Cliente.Id != null);

            if (user == null)
            {
                return Json(new
                {
                    success = false,
                    error = "Email não encontrado"
                });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var link =
                     $"{Request.Scheme}://{Request.Host}/" +
                     $"?mode=reset" +
                     $"&type=cliente" +
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

            <p>Se você não solicitou esta alteração, ignore este email.</p>"
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

    [HttpGet("ResetPassword")]
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
                    x.Cliente.Id != null);

            if (user == null)
            {
                return Json(new
                {
                    success = false,
                    error = "Usuário não encontrado"
                });
            }
            var token = Uri.UnescapeDataString(model.Token);
            var result = await _userManager.ResetPasswordAsync(
                user,
                model.Token,
                model.Password);

            if (result.Succeeded)
            {
                return Json(new
                {
                    success = true,
                    redirect = "/?login=true&type=cliente&reset=success"
                });
            }

            return Json(new
            {
                success = false,
                error = string.Join(
                    "<br>",
                    result.Errors.Select(x => x.Description))
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

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!User.Identity!.IsAuthenticated)
        {
            return Json(new
            {
                autenticado = false
            });
        }

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null || user.ClienteId == null)
        {
            return Json(new
            {
                autenticado = false
            });
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == user.ClienteId);

        if (cliente == null)
        {
            return Json(new
            {
                autenticado = false
            });
        }

        return Json(new
        {
            autenticado = true,
            clienteId = user.ClienteId,
            nome = cliente.Nome,
            telefone = cliente.Telefone
        });
    }


}