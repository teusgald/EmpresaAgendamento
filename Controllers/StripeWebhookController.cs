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
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            IStripeService stripeService,
            IConfiguration configuration,
            ILogger<StripeWebhookController> logger)
        {
            _stripeService = stripeService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            if (string.IsNullOrEmpty(webhookSecret))
            {
                // Ainda não configurado (falta registrar o endpoint no Stripe
                // e colar o whsec_... aqui) — não processa sem poder validar.
                return StatusCode(503, "Webhook secret não configurado.");
            }

            try
            {
                // Leitura do corpo também vai dentro do try: um corpo
                // truncado/corrompido (conexão caindo no meio do POST do
                // Stripe) não pode derrubar o endpoint com 500 cru.
                var json = await new StreamReader(Request.Body).ReadToEndAsync();

                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret);

                await _stripeService.ProcessarEventoAsync(stripeEvent);

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Webhook do Stripe rejeitado (assinatura/evento inválido).");
                return BadRequest();
            }
            catch (Exception ex)
            {
                // Nunca deixar escapar sem tratar: um 200/4xx "por acidente"
                // (via exception handler global) faria o Stripe achar que
                // processou e não tentar de novo. 500 explícito faz o Stripe
                // reenviar esse evento depois.
                _logger.LogError(ex, "Falha ao processar evento do Stripe.");
                return StatusCode(500);
            }
        }
    }
}
