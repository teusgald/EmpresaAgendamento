using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("assinatura")]
    [Authorize(Roles = "Empresa")]
    public class AssinaturaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStripeService _stripeService;
        private readonly IConfiguration _configuration;

        public AssinaturaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStripeService stripeService,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _stripeService = stripeService;
            _configuration = configuration;
        }

        private async Task<int?> GetEmpresaId()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                return user?.EmpresaId;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? tipoPlano)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var empresa = await _context.Empresas
                .FirstOrDefaultAsync(e => e.Id == empresaId);

            if (empresa == null)
            {
                ToastHelper.Error(TempData, "Empresa não encontrada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var temAssinaturaAtiva = empresa.AssinaturaStatus == "active" || empresa.AssinaturaStatus == "trialing";

            try
            {
                ViewBag.Plano = await _stripeService.GarantirPlanoPadraoAsync();
            }
            catch (Exception ex)
            {
                ToastHelper.Error(TempData, $"Não foi possível carregar os planos do Stripe: {ex.Message}");
            }

            // Plano escolhido: monta o Checkout embutido (mesmo formulário do
            // Stripe, mas dentro da própria página) em vez de redirecionar.
            if (!temAssinaturaAtiva && !string.IsNullOrEmpty(tipoPlano))
            {
                var urlRetorno = $"{Request.Scheme}://{Request.Host}/assinatura/retorno?session_id={{CHECKOUT_SESSION_ID}}";

                try
                {
                    ViewBag.ClientSecret = await _stripeService.CriarCheckoutClientSecretAsync(
                        empresaId.Value, tipoPlano, urlRetorno);
                    ViewBag.TipoPlanoSelecionado = tipoPlano;
                    ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                }
                catch (Exception ex)
                {
                    ToastHelper.Error(TempData, $"Erro ao iniciar o pagamento: {ex.Message}");
                }
            }

            return View(empresa);
        }

        // Chamado automaticamente quando a empresa loga sem assinatura ativa
        // (ver RequerAssinaturaAtivaFilter) — já manda direto pro pagamento do
        // plano que ela escolheu na página de vendas, sem pedir pra escolher de novo.
        [HttpGet("iniciar")]
        public async Task<IActionResult> Iniciar()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var empresa = await _context.Empresas
                .FirstOrDefaultAsync(e => e.Id == empresaId);

            var tipoPlano = string.IsNullOrEmpty(empresa?.TipoPlanoEscolhido)
                ? "mensal"
                : empresa.TipoPlanoEscolhido;

            return RedirectToAction(nameof(Index), new { tipoPlano });
        }

        // O Stripe redireciona o navegador pra cá quando o Checkout embutido
        // termina (sucesso ou expiração) — o webhook já deve ter atualizado o
        // status, aqui só confirmamos e mostramos o aviso certo pro usuário.
        [HttpGet("retorno")]
        public async Task<IActionResult> Retorno(string session_id)
        {
            try
            {
                var status = await _stripeService.ObterStatusCheckoutAsync(session_id);

                if (status == "complete")
                {
                    ToastHelper.Success(
                        TempData,
                        "Assinatura confirmada! Pode levar alguns segundos para refletir aqui.");
                }
                else
                {
                    ToastHelper.Warning(TempData, "Pagamento não concluído.");
                }
            }
            catch (Exception ex)
            {
                ToastHelper.Error(TempData, ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("portal")]
        public async Task<IActionResult> Portal()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var urlRetorno = $"{Request.Scheme}://{Request.Host}/assinatura";

            try
            {
                var url = await _stripeService.CriarPortalSessionAsync(empresaId.Value, urlRetorno);
                return Redirect(url);
            }
            catch (Exception ex)
            {
                ToastHelper.Error(TempData, ex.Message);
                return RedirectToAction(nameof(Index));
            }
        }

    }
}
