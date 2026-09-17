using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Filters
{
    // Módulos como Financeiro, Serviços e Fidelidade agora aceitam a role
    // Funcionario, mas só quem tem NivelAcesso = Gerente pode entrar — o
    // dono da empresa (role Empresa) nunca é bloqueado aqui.
    public class RequerGerenteFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public RequerGerenteFilter(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITempDataDictionaryFactory tempDataFactory)
        {
            _context = context;
            _userManager = userManager;
            _tempDataFactory = tempDataFactory;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.User.IsInRole("Funcionario"))
            {
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(context.HttpContext.User);

            var nivelAcesso = user == null
                ? (NivelAcessoFuncionario?)null
                : await _context.Funcionarios
                    .Where(f => f.UserId == user.Id)
                    .Select(f => (NivelAcessoFuncionario?)f.NivelAcesso)
                    .FirstOrDefaultAsync();

            if (nivelAcesso != NivelAcessoFuncionario.Gerente)
            {
                var tempData = _tempDataFactory.GetTempData(context.HttpContext);
                tempData["ToastMessage"] = "Esse módulo é liberado só pra gerentes.";
                tempData["ToastType"] = "warning";

                context.Result = new RedirectToActionResult("Index", "Agendamentos", null);
                return;
            }

            await next();
        }
    }
}
