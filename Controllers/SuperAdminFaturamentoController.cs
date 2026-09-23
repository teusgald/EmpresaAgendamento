using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    // Faturamento real (faturas pagas no Stripe) — não existe um livro-caixa
    // local paralelo pra isso, o Stripe é a fonte de verdade.
    [Authorize(Roles = "SuperAdmin")]
    [Route("superadmin/faturamento")]
    public class SuperAdminFaturamentoController : Controller
    {
        private readonly IStripeService _stripeService;
        private readonly ILogger<SuperAdminFaturamentoController> _logger;

        public SuperAdminFaturamentoController(IStripeService stripeService, ILogger<SuperAdminFaturamentoController> logger)
        {
            _stripeService = stripeService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string periodo = "mes", DateTime? dataInicial = null, DateTime? dataFinal = null)
        {
            var (inicio, fimExclusivo) = CalcularIntervalo(periodo, dataInicial, dataFinal);

            try
            {
                var (totalFaturas, valorTotal) = await _stripeService.ListarFaturasPagasAsync(inicio, fimExclusivo);

                var model = new SuperAdminFaturamentoViewModel
                {
                    Periodo = periodo,
                    DataInicial = inicio,
                    DataFinalExclusiva = fimExclusivo,
                    TotalFaturas = totalFaturas,
                    ValorTotal = valorTotal
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar faturas pagas no Stripe (período {Periodo}).", periodo);
                ToastHelper.Error(TempData, "Erro ao carregar dados de faturamento do Stripe.");

                return View(new SuperAdminFaturamentoViewModel
                {
                    Periodo = periodo,
                    DataInicial = inicio,
                    DataFinalExclusiva = fimExclusivo
                });
            }
        }

        private static (DateTime Inicio, DateTime FimExclusivo) CalcularIntervalo(
            string periodo, DateTime? dataInicial, DateTime? dataFinal)
        {
            if (periodo == "personalizado" && dataInicial.HasValue && dataFinal.HasValue)
            {
                return (dataInicial.Value.Date, dataFinal.Value.Date.AddDays(1));
            }

            var hoje = DateTime.Today;

            return periodo switch
            {
                "dia" => (hoje, hoje.AddDays(1)),

                // Semana começa na segunda-feira.
                "semana" => (
                    hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7)),
                    hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7)).AddDays(7)),

                "ano" => (
                    new DateTime(hoje.Year, 1, 1),
                    new DateTime(hoje.Year + 1, 1, 1)),

                // "mes" é o default.
                _ => (
                    new DateTime(hoje.Year, hoje.Month, 1),
                    new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1))
            };
        }
    }
}
