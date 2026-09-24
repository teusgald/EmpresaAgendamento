using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class EmpresaService : IEmpresaService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public EmpresaService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _context = context;
            _signInManager = signInManager;
        }

        private static readonly string[] NomesPlanoValidos = { "Start", "Pro", "Business" };

        public async Task<(bool Success, string Error, ApplicationUser? User)> RegisterAsync(
            EmpresaRegisterViewModel model, string? tipoPlanoEscolhido = null, string? nomePlanoEscolhido = null)
        {
            var empresaExistente = _userManager.Users
                .Any(x =>
                    x.Email == model.Email &&
                    x.EmpresaId != null);

            if (empresaExistente)
            {
                return (false, "Já existe uma empresa cadastrada com este e-mail.", null);
            }

            var user = new ApplicationUser
            {
                UserName = $"empresa-{Guid.NewGuid()}",
                Email = model.Email,
                NomeCompleto = model.NomeResponsavel,
                PhoneNumber = model.Telefone
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return (
                    false,
                    string.Join("<br>",
                        result.Errors.Select(x => x.Description)),
                    null
                );
            }

            await _userManager.AddToRoleAsync(user, "Empresa");

            var empresa = new Empresa
            {
                Nome = model.NomeEmpresa,
                EmailContato = model.Email,
                Telefone = model.Telefone,
                WhatsApp = model.Telefone,
                Categoria = model.Categoria,
                TipoPlanoEscolhido = tipoPlanoEscolhido is "anual" or "semestral" ? tipoPlanoEscolhido : "mensal",
                NomePlanoEscolhido = NomesPlanoValidos.Contains(nomePlanoEscolhido) ? nomePlanoEscolhido : "Start"
            };

            _context.Empresas.Add(empresa);
            await _context.SaveChangesAsync();

            user.EmpresaId = empresa.Id;

            await _userManager.UpdateAsync(user);

            // Sem sign-in automático — precisa confirmar o e-mail primeiro
            // (o controller envia o e-mail de confirmação com o usuário retornado aqui).
            return (true, null, user);
        }

        public async Task<(bool Success, string Error)> LoginAsync(EmpresaLoginViewModel model)
        {
            var users = await _userManager.Users
                .Where(u => u.Email == model.Email)
                .ToListAsync();

            ApplicationUser user = null;

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Empresa"))
                {
                    user = u;
                    break;
                }
            }

            if (user == null)
                return (false, "Empresa não encontrada.");

            // isPersistent: true — sem isso, o cookie de login não usa o
            // ExpireTimeSpan/SlidingExpiration configurado em
            // ConfigureApplicationCookie (Program.cs) e vira um cookie de
            // sessão, que o iOS costuma descartar ao reabrir o app instalado
            // (PWA), fazendo a sessão "expirar" a cada abertura.
            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, true, true);

            if (!result.Succeeded)
            {
                string erro;

                if (result.IsLockedOut)
                {
                    erro = "Muitas tentativas de login. Tente novamente em alguns minutos.";
                }
                else if (result.IsNotAllowed)
                {
                    erro = "Confirme seu e-mail antes de entrar. Verifique sua caixa de entrada.";
                }
                else
                {
                    erro = "Email ou senha inválidos.";
                }

                return (false, erro);
            }

            return (true, null);
        }
    }
}
