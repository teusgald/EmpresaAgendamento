using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace EmpresaAgendamento.Controllers
{
    // Endpoint chamado pelo Stripe, não por um usuário logado — a segurança
    // aqui vem da verificação de assinatura (Stripe-Signature), não de
    // Authorize/CSRF, que não fazem sentido pra uma chamada servidor-a-servidor.
    [Route("stripe/webhook")]
    [ApiController]
    [AllowAnonymous]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IStripeService _stripeService;
        private readonly IConfiguration _configuration;

        public StripeWebhookController(IStripeService stripeService, IConfiguration configuration)
        {
            _stripeService = stripeService;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            if (string.IsNullOrEmpty(webhookSecret))
            {
                // Ainda não configurado (falta registrar o endpoint no Stripe
                // e colar o whsec_... aqui) — não processa sem poder validar.
                return StatusCode(503, "Webhook secret não configurado.");
            }

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret);

                await _stripeService.ProcessarEventoAsync(stripeEvent);

                return Ok();
            }
            catch (StripeException)
            {
                return BadRequest();
            }
        }
    }
}
