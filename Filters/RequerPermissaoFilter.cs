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
    // Sucessor granular do RequerGerenteFilter — em vez de travar o
    // controller inteiro pra quem não é Gerente, checa uma ação específica
    // (Visualizar/Criar/Editar/Excluir) dentro de um módulo específico,
    // conforme o Perfil configurável atribuído ao funcionário. Dono da
    // empresa (role Empresa) nunca é bloqueado aqui, igual ao antecessor.
    //
    // Também expõe, via ViewBag, as 4 permissões do módulo inteiro (não só a
    // ação que esse filtro específico está checando) — assim a view (ex.:
    // Index) consegue esconder botão de Criar/Editar/Excluir que o
    // funcionário não tem, sem cada controller precisar buscar isso de novo.
    public class RequerPermissaoFilter : IAsyncActionFilter
    {
        private readonly string _modulo;
        private readonly string _acao;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public RequerPermissaoFilter(
            string modulo,
            string acao,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITempDataDictionaryFactory tempDataFactory)
        {
            _modulo = modulo;
            _acao = acao;
            _context = context;
            _userManager = userManager;
            _tempDataFactory = tempDataFactory;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.User.IsInRole("Funcionario"))
            {
                // Dono da empresa nunca é restrito — todas as 4 ações liberadas.
                ExporVisibilidade(context, true, true, true, true);
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(context.HttpContext.User);

            var perfilId = user == null
                ? null
                : await _context.Funcionarios
                    .Where(f => f.UserId == user.Id)
                    .Select(f => f.PerfilId)
                    .FirstOrDefaultAsync();

            PerfilPermissao? permissao = null;

            if (perfilId != null)
            {
                permissao = await _context.PerfilPermissoes
                    .FirstOrDefaultAsync(p => p.PerfilId == perfilId && p.Modulo == _modulo);
            }

            ExporVisibilidade(
                context,
                permissao?.PodeVisualizar ?? false,
                permissao?.PodeCriar ?? false,
                permissao?.PodeEditar ?? false,
                permissao?.PodeExcluir ?? false);

            // Sem linha de permissão pro módulo = sem acesso (falha fechada,
            // não aberta) — funcionário sem Perfil atribuído também cai aqui.
            var liberado = permissao != null && _acao switch
            {
                "Visualizar" => permissao.PodeVisualizar,
                "Criar" => permissao.PodeCriar,
                "Editar" => permissao.PodeEditar,
                "Excluir" => permissao.PodeExcluir,
                _ => false
            };

            if (!liberado)
            {
                var tempData = _tempDataFactory.GetTempData(context.HttpContext);
                tempData["ToastMessage"] = "Seu perfil de acesso não permite essa ação.";
                tempData["ToastType"] = "warning";

                context.Result = new RedirectToActionResult("Index", "Agendamentos", null);
                return;
            }

            await next();
        }

        private static void ExporVisibilidade(ActionExecutingContext context, bool ver, bool criar, bool editar, bool excluir)
        {
            if (context.Controller is Controller controller)
            {
                controller.ViewBag.PodeVisualizarModulo = ver;
                controller.ViewBag.PodeCriarModulo = criar;
                controller.ViewBag.PodeEditarModulo = editar;
                controller.ViewBag.PodeExcluirModulo = excluir;
            }
        }
    }
}
