using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Filters
{
    // Bloqueia o acesso ao módulo financeiro quando o plano da empresa não o
    // inclui (Plano.PermiteFinanceiro). Empresas sem plano vinculado (comum
    // em ambiente de teste/prototipagem) são liberadas por padrão, pra não
    // travar quem já está usando o financeiro sem ter um plano configurado.
    public class RequerPlanoFinanceiroFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public RequerPlanoFinanceiroFilter(
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
            var user = await _userManager.GetUserAsync(context.HttpContext.User);

            if (user?.EmpresaId == null)
            {
                context.Result = new RedirectToActionResult("Login", "EmpresaAuth", null);
                return;
            }

            var permiteFinanceiro = await _context.Empresas
                .Where(e => e.Id == user.EmpresaId)
                .Select(e => e.Plano == null || e.Plano.PermiteFinanceiro)
                .FirstOrDefaultAsync();

            if (!permiteFinanceiro)
            {
                var tempData = _tempDataFactory.GetTempData(context.HttpContext);
                tempData["ToastMessage"] = "Seu plano atual não inclui o módulo financeiro.";
                tempData["ToastType"] = "warning";

                context.Result = new RedirectToActionResult("Dashboard", "Empresas", null);
                return;
            }

            await next();
        }
    }
}
