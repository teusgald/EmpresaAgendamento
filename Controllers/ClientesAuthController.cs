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
    public async Task<IActionResult> Login(ClienteLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // 🔹 Busca todos os usuários com o email informado
        var users = await _userManager.Users
            .Where(u => u.Email == model.Email)
            .ToListAsync();

        // 🔹 Filtra pelo usuário que é Cliente
        ApplicationUser user = null;
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
            ModelState.AddModelError("", "Usuário não encontrado ou não é cliente.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
                return Json(new
                {
                    success = true,
                    redirect = "/Cliente/Agendamentos"
                });

        ModelState.AddModelError("", "Email ou senha inválidos.");
        return View(model);
    }

    // =========================
    // REGISTER
    // =========================
    [HttpGet("registro")]
    public IActionResult Register() => View();

    [HttpPost("registro")]
    public async Task<IActionResult> Register(ClienteRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                error = "Preencha todos os campos corretamente."
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

        user.Cliente.Id = cliente.Id;

        await _userManager.UpdateAsync(user);

        // =====================================
        // EMAIL BOAS VINDAS
        // =====================================

        bool emailEnviado = false;
        string erroEmail = "";

        try
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "Bem-vindo ao Sistema",
                $@"
            <h2>Olá {cliente.Nome}</h2>

            <p>Sua conta foi criada com sucesso.</p>

            <p>Agora você já pode acessar o sistema e realizar seus agendamentos.</p>
            ");

            emailEnviado = true;
        }
        catch (Exception ex)
        {
            erroEmail = ex.Message;
        }

        // =====================================
        // LOGIN AUTOMÁTICO
        // =====================================

        await _signInManager.SignInAsync(
            user,
            isPersistent: false);

        return Json(new
        {
            success = true,
            redirect = "/Cliente/Agendamentos",
            emailEnviado,
            erroEmail
        });
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
}