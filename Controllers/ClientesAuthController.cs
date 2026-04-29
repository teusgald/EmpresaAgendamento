using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
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
            return RedirectToAction("Index", "AgendamentosClientes");

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
            return View(model);

        // 🔹 Cria usuário com UserName único (não importa se email existe em outra role)
        var user = new ApplicationUser
        {
            UserName = $"cliente-{Guid.NewGuid()}",
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        // 🔹 Adiciona role Cliente
        await _userManager.AddToRoleAsync(user, "Cliente");

        // 🔹 Cria entidade Cliente
        var cliente = new Cliente
        {
            Nome = model.Nome,
            Email = model.Email,
            Telefone = model.Telefone,
            UserId = user.Id
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        // 🔹 Vincula ClienteId ao usuário
        user.ClienteId = cliente.Id;
        await _userManager.UpdateAsync(user);

        // 🔹 Envio de email de boas-vindas
        try
        {
            string subject = "Bem-vindo ao Sistema de Agendamento!";
            string message = $@"
                Olá {cliente.Nome},<br/><br/>
                Parabéns! Sua conta de cliente foi criada com sucesso.<br/>
                Agora você pode acessar o sistema e agendar seus serviços.<br/><br/>
                Atenciosamente,<br/>
                Equipe EmpresaAgendamento
            ";
            await _emailService.SendEmailAsync(user.Email, subject, message);
        }
        catch (Exception ex)
        {
            // Apenas loga o erro sem quebrar o fluxo
            Console.WriteLine($"Erro ao enviar email: {ex.Message}");
        }

        // 🔹 Login automático
        await _signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "AgendamentosClientes");
    }

    // =========================
    // LOGOUT
    // =========================
    
    public async Task<IActionResult> Logout([FromServices] SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}