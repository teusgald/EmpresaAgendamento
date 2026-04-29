using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            ApplicationDbContext context,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Nome da Empresa")]
            public string NomeEmpresa { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Senha")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar Senha")]
            [Compare("Password", ErrorMessage = "As senhas não coincidem.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid) return Page();

            // 1️⃣ Criar empresa
            var empresa = new Empresa
            {
                Nome = Input.NomeEmpresa,
                EmailContato = Input.Email,
                Email = Input.Email
            };

            _context.Empresas.Add(empresa);
            await _context.SaveChangesAsync();

            // 2️⃣ Criar usuário com UserName único
            var user = new ApplicationUser
            {
                UserName = $"empresa-{Guid.NewGuid()}",
                Email = Input.Email,
                EmpresaId = empresa.Id
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                // 3️⃣ Adicionar role Empresa
                await _userManager.AddToRoleAsync(user, "Empresa");

                // 4️⃣ Envio de email de boas-vindas
                try
                {
                    string subject = "Bem-vindo ao Sistema de Agendamento!";
                    string message = $@"
                        Olá {empresa.Nome},<br/><br/>
                        Parabéns! Sua conta de empresa foi criada com sucesso.<br/>
                        Agora você pode acessar o sistema e gerenciar sua empresa.<br/><br/>
                        Atenciosamente,<br/>
                        Equipe EmpresaAgendamento
                    ";
                    await _emailService.SendEmailAsync(user.Email, subject, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao enviar email: {0}", ex.Message);
                }

                // 5️⃣ Login automático
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            // Se deu erro, remove empresa
            _context.Empresas.Remove(empresa);
            await _context.SaveChangesAsync();

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }
    }
}