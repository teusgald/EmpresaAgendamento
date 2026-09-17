using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

[Route("cliente")]
public class ClientesAuthController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientesAuthController> _logger;

    public ClientesAuthController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ClientesAuthController> logger)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    // =========================
    // LOGIN
    // =========================
    [HttpGet("login")]
    public IActionResult Login() => View();

    // Sempre responde em JSON — todo lugar que chama isso (home e a página
    // pública da empresa) manda a requisição via fetch()/AJAX e espera JSON
    // de volta, nunca um formulário nativo. Antes, fora do fluxo "publico"
    // essa ação devolvia um RedirectToAction (uma resposta HTML de
    // verdade) — o fetch().then(res => res.json()) do Home/index.cshtml
    // não sabe ler isso e quebrava com erro de requisição, mesmo com a
    // senha certa. O Home/index nunca manda o campo "Origem", só a página
    // pública manda — por isso só quebrava a partir do Home.
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(ClienteLoginViewModel model)
    {
        var origem = Request.Form["Origem"].ToString();

        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                error = "Dados inválidos."
            });
        }

        // Mesmo e-mail pode estar cadastrado tanto como Empresa quanto como
        // Cliente (são contas separadas) — por isso filtra pela role certa
        // em vez de assumir que o primeiro resultado é o certo.
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
            // Mesma mensagem de senha errada — não dá pra revelar se o
            // e-mail tem conta de cliente só pelo texto do erro de login.
            _logger.LogWarning("Login de cliente falhou (e-mail não encontrado): {Email}.", model.Email);

            return Json(new
            {
                success = false,
                error = "Email ou senha inválidos."
            });
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

            _logger.LogWarning("Login de cliente falhou para o e-mail {Email}: {Motivo}.", model.Email, mensagemErro);

            return Json(new
            {
                success = false,
                error = mensagemErro
            });
        }

        // LOGIN VIA MODAL PÚBLICO — fica na própria página (só recarrega),
        // não manda pro portal do cliente. Quem abriu o login pode estar no
        // meio de um agendamento ou de uma avaliação; sair da página perdia
        // esse contexto.
        if (origem == "publico")
        {
            return Json(new
            {
                success = true,
                reload = true
            });
        }

        // LOGIN NORMAL (home ou qualquer outro lugar) — manda pro portal do cliente.
        return Json(new
        {
            success = true,
            redirect = "/Cliente/Agendamentos"
        });
    }

    // =========================
    // REGISTER
    // =========================
    [HttpGet("registro")]
    public IActionResult Register() => View();

    [HttpPost("registro")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
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
                    x.Cliente != null);

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
                $"{LinkBaseHelper.ObterBase(Request, _configuration)}/cliente/confirmar-email" +
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
            _logger.LogError(ex, "Falha ao enviar e-mail de confirmação de cadastro pro cliente {Email}.", user.Email);
            erroEmail = "Não foi possível enviar o e-mail de confirmação agora.";
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout([FromServices] SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Redirect("/?login=true&type=cliente");
    }

    [HttpGet("forgot")]
    public IActionResult Forgot() => View();

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
                    x.Cliente != null);

            // Resposta sempre igual, exista ou não a conta — senão dá pra
            // descobrir quais e-mails têm cadastro só testando esse formulário.
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var link =
                         $"{LinkBaseHelper.ObterBase(Request, _configuration)}/" +
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
            }
            else
            {
                _logger.LogInformation("Recuperação de senha solicitada para e-mail de cliente não cadastrado: {Email}.", model.Email);
            }

            return Json(new
            {
                success = true,
                message = "Se esse e-mail estiver cadastrado, enviamos um link de recuperação para ele."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar recuperação de senha (cliente, e-mail {Email}).", model.Email);

            return Json(new
            {
                success = false,
                error = "Não foi possível enviar o e-mail de recuperação agora. Tente novamente em alguns instantes."
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
            // "token inválido/expirado" — do contrário, dava pra descobrir se
            // um e-mail tem conta de cliente só testando esse formulário.
            const string erroGenerico = "Não foi possível redefinir a senha. O link pode ter expirado — solicite um novo.";

            var user = _userManager.Users
                .FirstOrDefault(x =>
                    x.Email == model.Email &&
                    x.Cliente != null);

            if (user == null)
            {
                _logger.LogWarning("Tentativa de redefinir senha de cliente com e-mail não cadastrado: {Email}.", model.Email);

                return Json(new { success = false, error = erroGenerico });
            }

            var result = await _userManager.ResetPasswordAsync(
                user,
                model.Token,
                model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Senha redefinida com sucesso (cliente, e-mail {Email}).", model.Email);

                return Json(new
                {
                    success = true,
                    redirect = "/?login=true&type=cliente&reset=success"
                });
            }

            _logger.LogWarning(
                "Falha ao redefinir senha de cliente para {Email}: {Erros}.",
                model.Email,
                string.Join("; ", result.Errors.Select(x => x.Code)));

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
            _logger.LogError(ex, "Falha ao redefinir senha (cliente, e-mail {Email}).", model.Email);

            return Json(new
            {
                success = false,
                error = "Não foi possível redefinir sua senha agora. Tente novamente em alguns instantes."
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