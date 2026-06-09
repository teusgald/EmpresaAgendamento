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

        public async Task<(bool Success, string Error)> RegisterAsync(EmpresaRegisterViewModel model)
        {
            var empresaExistente = _userManager.Users
                .Any(x =>
                    x.Email == model.Email &&
                    x.EmpresaId != null);

            if (empresaExistente)
            {
                return (false, "Já existe uma empresa cadastrada com este e-mail.");
            }

            var user = new ApplicationUser
            {
                UserName = $"empresa-{Guid.NewGuid()}",
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return (
                    false,
                    string.Join("<br>",
                        result.Errors.Select(x => x.Description))
                );
            }

            await _userManager.AddToRoleAsync(user, "Empresa");

            var empresa = new Empresa
            {
                Nome = model.NomeEmpresa,
                EmailContato = model.Email
            };

            _context.Empresas.Add(empresa);
            await _context.SaveChangesAsync();

            user.EmpresaId = empresa.Id;

            await _userManager.UpdateAsync(user);

            await _signInManager.SignInAsync(user, false);

            return (true, null);
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

            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, false, false);

            if (!result.Succeeded)
                return (false, "Email ou senha inválidos.");

            return (true, null);
        }
    }
}
