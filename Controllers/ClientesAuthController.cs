using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Route("cliente")]
public class ClientesAuthController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IClienteUnificacaoService _clienteUnificacaoService;
    private readonly ILogger<ClientesAuthController> _logger;

    public ClientesAuthController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        IConfiguration configuration,
        IClienteUnificacaoService clienteUnificacaoService,
        ILogger<ClientesAuthController> logger)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _configuration = configuration;
        _clienteUnificacaoService = clienteUnificacaoService;
        _logger = logger;
    }

    // =========================
    // 🔥 LOGIN COM GOOGLE
    // =========================
    [HttpGet("login/google")]
    public IActionResult LoginGoogle()
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "ClientesAuth");
        var properties = _signInManager.ConfigureExternalAuthenticationProperties("GoogleCliente", redirectUrl);
        return Challenge(properties, "GoogleCliente");
    }

    [HttpGet("login/google-callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();

        if (info == null)
        {
            ToastHelper.Error(TempData, "Não foi possível entrar com o Google. Tente novamente.");
            return RedirectToAction("Index", "Home");
        }

        // Login repetido — conta já linkada ao Google.
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            return RedirectToAction("Index", "ClienteInicio");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var emailVerificado = info.Principal.FindFirstValue("email_verified");
        var nome = info.Principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(email) || emailVerificado == "false")
        {
            ToastHelper.Error(TempData, "Não conseguimos confirmar seu e-mail do Google. Tente novamente.");
            return RedirectToAction("Index", "Home");
        }

        // Já existe conta local (senha) com esse e-mail — só linka o Google a ela.
        var usuarioExistente = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Cliente != null);

        if (usuarioExistente != null)
        {
            await _userManager.AddLoginAsync(usuarioExistente, info);
            await _signInManager.SignInAsync(usuarioExistente, isPersistent: true);
            return RedirectToAction("Index", "ClienteInicio");
        }

        // Cadastro "órfão" (feito manualmente por alguma empresa, sem login
        // próprio) com esse e-mail — ativa em vez de duplicar, mesma lógica
        // do "Esqueci minha senha" (ClientesAuthController.Forgot).
        var clienteParaAtivar = await _clienteUnificacaoService.UnificarOrfaosAsync(email);

        ApplicationUser novoUsuario;

        if (clienteParaAtivar != null)
        {
            novoUsuario = new ApplicationUser
            {
                UserName = $"cliente-{Guid.NewGuid()}",
                Email = email,
                NomeCompleto = clienteParaAtivar.Nome,
                EmailConfirmed = true
            };

            var criarResult = await _userManager.CreateAsync(novoUsuario);

            if (!criarResult.Succeeded)
            {
                _logger.LogError(
                    "Falha ao ativar conta via Google pro cliente {ClienteId} (e-mail {Email}): {Erros}.",
                    clienteParaAtivar.Id, email, string.Join("; ", criarResult.Errors.Select(e => e.Description)));

                ToastHelper.Error(TempData, "Não foi possível entrar agora. Tente novamente.");
                return RedirectToAction("Index", "Home");
            }

            await _userManager.AddToRoleAsync(novoUsuario, "Cliente");

            clienteParaAtivar.UserId = novoUsuario.Id;
            novoUsuario.ClienteId = clienteParaAtivar.Id;

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(novoUsuario);
        }
        else
        {
            // Pessoa nova de verdade — cria Cliente igual ao Register local,
            // só que sem senha (login é só via Google) e já com e-mail
            // confirmado (o Google já provou a posse da caixa de entrada).
            novoUsuario = new ApplicationUser
            {
                UserName = $"cliente-{Guid.NewGuid()}",
                Email = email,
                NomeCompleto = nome,
                EmailConfirmed = true
            };

            var criarResult = await _userManager.CreateAsync(novoUsuario);

            if (!criarResult.Succeeded)
            {
                _logger.LogError(
                    "Falha ao criar conta via Google pro e-mail {Email}: {Erros}.",
                    email, string.Join("; ", criarResult.Errors.Select(e => e.Description)));

                ToastHelper.Error(TempData, "Não foi possível criar sua conta agora. Tente novamente.");
                return RedirectToAction("Index", "Home");
            }

            await _userManager.AddToRoleAsync(novoUsuario, "Cliente");

            var cliente = new Cliente
            {
                Nome = string.IsNullOrWhiteSpace(nome) ? "Cliente" : nome,
                Email = email,
                UserId = novoUsuario.Id
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            novoUsuario.ClienteId = cliente.Id;
            await _userManager.UpdateAsync(novoUsuario);
        }

        await _userManager.AddLoginAsync(novoUsuario, info);
        await _signInManager.SignInAsync(novoUsuario, isPersistent: true);

        return RedirectToAction("Index", "ClienteInicio");
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
        try
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

            // isPersistent: true — sem isso o cookie de login é "de sessão" e
            // não respeita o ExpireTimeSpan/SlidingExpiration configurado em
            // Program.cs; no PWA instalado do cliente (iOS), isso fazia a
            // sessão "expirar" toda vez que a pessoa reabria o app.
            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                true,
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

            // LOGIN NORMAL (home ou qualquer outro lugar) — manda pra tela
            // inicial do portal do cliente (sugestão de empresas).
            return Json(new
            {
                success = true,
                redirect = "/Cliente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar login de cliente para o e-mail {Email}.", model.Email);

            return Json(new
            {
                success = false,
                error = "Não foi possível entrar agora. Tente novamente em alguns instantes."
            });
        }
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
        try
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

            // Cliente cadastrado por alguma empresa (sem login próprio) com
            // esse mesmo e-mail — em vez de criar um segundo cadastro do
            // zero, a pessoa precisa "ativar" o que já existe (mesmo link de
            // definir senha do "Esqueci minha senha") pra não perder o
            // histórico de agendamentos que já tem com essa(s) empresa(s).
            var temCadastroParaAtivar = await _context.Clientes
                .AnyAsync(c => c.Email == model.Email && c.UserId == null);

            if (temCadastroParaAtivar)
            {
                return Json(new
                {
                    success = false,
                    error = "Já existe um cadastro com esse e-mail em alguma empresa que você já visitou. " +
                        "Clique em \"Esqueci minha senha\" pra definir uma senha e ver seu histórico."
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar cadastro de cliente para o e-mail {Email}.", model.Email);

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

            return Redirect(result.Succeeded
                ? "/?login=true&type=cliente&confirmado=true"
                : "/?login=true&type=cliente&confirmado=false");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao confirmar e-mail de cliente (userId {UserId}).", userId);
            return Redirect("/?login=true&type=cliente&confirmado=false");
        }
    }

    // =========================
    // LOGOUT
    // =========================
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout([FromServices] SignInManager<ApplicationUser> signInManager)
    {
        try
        {
            await signInManager.SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao encerrar sessão de cliente.");
        }

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

            // Sem conta própria ainda, mas existe cadastro feito por alguma
            // empresa (Cliente sem UserId) com esse e-mail — "ativa" agora:
            // cria a conta de login por trás dos panos (sem senha
            // utilizável ainda) e une os cadastros duplicados, se houver
            // mais de uma empresa que já cadastrou essa pessoa. A senha de
            // verdade só é definida quando o link deste e-mail for usado.
            var ativandoConta = false;

            if (user == null)
            {
                var clienteParaAtivar = await _clienteUnificacaoService.UnificarOrfaosAsync(model.Email);

                if (clienteParaAtivar != null)
                {
                    user = new ApplicationUser
                    {
                        UserName = $"cliente-{Guid.NewGuid()}",
                        Email = model.Email,
                        NomeCompleto = clienteParaAtivar.Nome,
                        EmailConfirmed = false
                    };

                    // Senha aleatória descartável — a pessoa nunca a vê, ela
                    // define a própria logo abaixo (mesmo padrão usado em
                    // FuncionariosController pro convite de funcionário).
                    var senhaDescartavel = Guid.NewGuid().ToString("N") + "Aa1!";

                    var resultCriacao = await _userManager.CreateAsync(user, senhaDescartavel);

                    if (resultCriacao.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Cliente");

                        clienteParaAtivar.UserId = user.Id;
                        user.ClienteId = clienteParaAtivar.Id;

                        await _context.SaveChangesAsync();
                        await _userManager.UpdateAsync(user);

                        ativandoConta = true;
                    }
                    else
                    {
                        _logger.LogError(
                            "Falha ao criar conta de ativação pro cliente {ClienteId} (e-mail {Email}): {Erros}.",
                            clienteParaAtivar.Id, model.Email,
                            string.Join("; ", resultCriacao.Errors.Select(e => e.Description)));

                        user = null;
                    }
                }
            }

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

                var assunto = ativandoConta ? "Seu cadastro já existe — ative sua conta" : "Recuperação de Senha";

                var corpo = ativandoConta
                    ? $@"
                <h2>Encontramos seu cadastro!</h2>

                <p>Você já tem agendamentos e histórico registrados com a gente. Defina uma senha pra acessar sua conta e ver tudo:</p>

                <p>
                    <a href='{link}'>
                        Clique aqui para definir sua senha
                    </a>
                </p>

                <p>Se você não reconhece isso, ignore este email.</p>"
                    : $@"
                <h2>Recuperação de Senha</h2>

                <p>Recebemos uma solicitação para redefinir sua senha.</p>

                <p>
                    <a href='{link}'>
                        Clique aqui para redefinir sua senha
                    </a>
                </p>

                <p>Se você não solicitou esta alteração, ignore este email.</p>";

                await _emailService.SendEmailAsync(model.Email, assunto, corpo);
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
                // Clicar num link mandado por e-mail e conseguir definir a
                // senha já prova que a pessoa tem acesso a essa caixa de
                // entrada — conta como confirmação de e-mail (cobre tanto o
                // "esqueci a senha" normal quanto a ativação de conta criada
                // a partir de um cadastro feito pela empresa).
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                }

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

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar dados do cliente logado.");

            return Json(new
            {
                autenticado = false
            });
        }
    }


}