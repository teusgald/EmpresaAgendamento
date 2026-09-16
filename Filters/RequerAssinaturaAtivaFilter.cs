using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Filters
{
    // Bloqueia o acesso ao portal da empresa (agendamentos, financeiro,
    // cadastros etc.) enquanto não houver assinatura ativa no Stripe — sem
    // período de teste. É uma lista de permissão (allow-list) dos
    // Controllers que fazem parte do "portal pago": qualquer Controller novo
    // que eu esquecer aqui simplesmente não é bloqueado (falha aberta, não
    // trava nada por engano), então revisar esta lista ao criar módulos novos.
    public class RequerAssinaturaAtivaFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> ControllersComPaywall = new(StringComparer.OrdinalIgnoreCase)
        {
            "Agendamentos",
            "Servicos",
            "Funcionarios",
            "Clientes",
            "Empresas",
            "ContasReceber",
            "ContasPagar",
            "Comissoes",
            "CategoriasFinanceiras",
            "Financeiro"
        };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequerAssinaturaAtivaFilter(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpUser = context.HttpContext.User;

            // Funcionário usa a mesma trava: se a empresa dele parar de pagar,
            // o acesso dele cai junto (ele também tem EmpresaId preenchido).
            if (httpUser.Identity?.IsAuthenticated != true ||
                (!httpUser.IsInRole("Empresa") && !httpUser.IsInRole("Funcionario")))
            {
                await next();
                return;
            }

            var controllerName = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;

            if (controllerName == null || !ControllersComPaywall.Contains(controllerName))
            {
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(httpUser);

            if (user?.EmpresaId == null)
            {
                await next();
                return;
            }

            var assinaturaStatus = await _context.Empresas
                .Where(e => e.Id == user.EmpresaId)
                .Select(e => e.AssinaturaStatus)
                .FirstOrDefaultAsync();

            var assinaturaAtiva = assinaturaStatus == "active" || assinaturaStatus == "trialing";

            if (!assinaturaAtiva)
            {
                // Funcionário não mexe em pagamento/assinatura (isso é só do
                // dono da empresa) — manda pra uma página de aviso, não pro
                // checkout.
                context.Result = new RedirectResult(
                    httpUser.IsInRole("Funcionario")
                        ? "/funcionario/bloqueado"
                        : "/assinatura/iniciar");

                return;
            }

            await next();
        }
    }
}
