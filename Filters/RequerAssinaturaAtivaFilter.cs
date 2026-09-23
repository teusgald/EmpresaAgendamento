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
            "Financeiro",
            "PlanosServico"
        };

        // Mesmo dentro de um Controller com paywall, essas ações pontuais
        // ficam sempre liberadas — dão uma tela inicial pro usuário novo
        // enquanto ele não paga (só Dashboard, o resto de Empresas continua
        // bloqueado).
        private static readonly HashSet<(string Controller, string Action)> AcoesLiberadas =
            new()
            {
                ("Empresas", "Dashboard")
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

            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var controllerName = actionDescriptor?.ControllerName;
            var actionName = actionDescriptor?.ActionName;

            if (controllerName == null || !ControllersComPaywall.Contains(controllerName))
            {
                await next();
                return;
            }

            if (actionName != null && AcoesLiberadas.Contains((controllerName, actionName)))
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

            var empresaInfo = await _context.Empresas
                .Where(e => e.Id == user.EmpresaId)
                .Select(e => new { e.AssinaturaStatus, e.VipAcesso })
                .FirstOrDefaultAsync();

            // VIP (liberado manualmente pelo painel do dono do sistema, ex.:
            // testers iniciais) nunca é bloqueado pelo paywall, independente
            // do status da assinatura no Stripe.
            var assinaturaAtiva =
                empresaInfo?.VipAcesso == true ||
                empresaInfo?.AssinaturaStatus == "active" ||
                empresaInfo?.AssinaturaStatus == "trialing";

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
