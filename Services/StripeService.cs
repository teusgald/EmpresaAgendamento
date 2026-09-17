using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace EmpresaAgendamento.Services
{
    public class StripeService : IStripeService
    {
        private readonly ApplicationDbContext _context;

        // Preços definidos com o dono do produto: mensal R$19,99, promoção de
        // R$9,90 nos 3 primeiros meses (cupom), anual R$199,90.
        private const decimal ValorMensal = 19.99m;
        private const decimal ValorMensalPromocional = 9.90m;
        private const decimal ValorAnual = 199.90m;
        private const string NomePlanoPadrao = "Plano Padrão";
        private const string Moeda = "brl";

        public StripeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Plano> GarantirPlanoPadraoAsync()
        {
            var plano = await _context.Planos
                .FirstOrDefaultAsync(p => p.Nome == NomePlanoPadrao);

            if (plano != null &&
                !string.IsNullOrEmpty(plano.StripePriceIdMensal) &&
                !string.IsNullOrEmpty(plano.StripePriceIdAnual))
            {
                return plano;
            }

            var productService = new ProductService();
            var product = await productService.CreateAsync(new ProductCreateOptions
            {
                Name = "Simpli Time — " + NomePlanoPadrao,
                Description = "Assinatura do sistema de agendamentos Simpli Time."
            });

            var priceService = new PriceService();

            var priceMensal = await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                Currency = Moeda,
                UnitAmount = (long)(ValorMensal * 100),
                Recurring = new PriceRecurringOptions { Interval = "month" }
            });

            var priceAnual = await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                Currency = Moeda,
                UnitAmount = (long)(ValorAnual * 100),
                Recurring = new PriceRecurringOptions { Interval = "year" }
            });

            var couponService = new CouponService();

            var cupomPromocional = await couponService.CreateAsync(new CouponCreateOptions
            {
                Name = "Promoção 3 primeiros meses",
                Currency = Moeda,
                AmountOff = (long)((ValorMensal - ValorMensalPromocional) * 100),
                Duration = "repeating",
                DurationInMonths = 3
            });

            if (plano == null)
            {
                plano = new Plano
                {
                    Nome = NomePlanoPadrao,
                    ValorMensal = ValorMensal,
                    ValorAnual = ValorAnual,
                    LimiteFuncionarios = 0,
                    LimiteAgendamentosMes = 0,
                    PermiteFinanceiro = true,
                    PermiteWhatsapp = true,
                    PermiteRelatorios = true,
                    PermiteMultiFuncionarios = true,
                    PermiteLandingPage = true,
                    PermiteAPI = false,
                    Ativo = true
                };

                _context.Planos.Add(plano);
            }

            plano.StripePriceIdMensal = priceMensal.Id;
            plano.StripePriceIdAnual = priceAnual.Id;
            plano.StripeCouponIdPromocional = cupomPromocional.Id;

            await _context.SaveChangesAsync();

            return plano;
        }

        // Endereço só entra se houver pelo menos algum dado — não faz sentido
        // mandar um AddressOptions todo vazio pro Stripe.
        private static AddressOptions? MontarEnderecoStripe(Empresa empresa)
        {
            if (string.IsNullOrWhiteSpace(empresa.Endereco) &&
                string.IsNullOrWhiteSpace(empresa.Cidade) &&
                string.IsNullOrWhiteSpace(empresa.CEP))
            {
                return null;
            }

            var linha1 = string.Join(", ", new[] { empresa.Endereco, empresa.Numero }
                .Where(p => !string.IsNullOrWhiteSpace(p)));

            return new AddressOptions
            {
                Line1 = string.IsNullOrWhiteSpace(linha1) ? null : linha1,
                Line2 = string.IsNullOrWhiteSpace(empresa.Complemento) ? empresa.Bairro : empresa.Complemento,
                City = empresa.Cidade,
                State = empresa.UF,
                PostalCode = empresa.CEP,
                Country = "BR"
            };
        }

        private async Task<string> ObterOuCriarStripeCustomerIdAsync(Empresa empresa)
        {
            var customerService = new CustomerService();

            // Mantém os dados do cliente Stripe sempre sincronizados com o
            // cadastro da empresa — antes, uma vez criado, o Customer nunca
            // era atualizado, então telefone/endereço preenchidos depois
            // nunca apareciam no Portal de faturamento.
            if (!string.IsNullOrEmpty(empresa.StripeCustomerId))
            {
                await customerService.UpdateAsync(empresa.StripeCustomerId, new CustomerUpdateOptions
                {
                    Email = empresa.EmailContato ?? empresa.Email,
                    Name = empresa.NomeFantasia ?? empresa.Nome,
                    Phone = empresa.Telefone ?? empresa.WhatsApp,
                    Address = MontarEnderecoStripe(empresa)
                });

                return empresa.StripeCustomerId;
            }

            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = empresa.EmailContato ?? empresa.Email,
                Name = empresa.NomeFantasia ?? empresa.Nome,
                Phone = empresa.Telefone ?? empresa.WhatsApp,
                Address = MontarEnderecoStripe(empresa),
                Metadata = new Dictionary<string, string>
                {
                    ["empresaId"] = empresa.Id.ToString()
                }
            });

            empresa.StripeCustomerId = customer.Id;
            await _context.SaveChangesAsync();

            return customer.Id;
        }

        public async Task<string> CriarCheckoutClientSecretAsync(
            int empresaId, string tipoPlano, string urlRetorno)
        {
            var empresa = await _context.Empresas
                .Include(e => e.Plano)
                .FirstOrDefaultAsync(e => e.Id == empresaId);

            if (empresa == null)
            {
                throw new InvalidOperationException("Empresa não encontrada.");
            }

            var plano = empresa.Plano ?? await GarantirPlanoPadraoAsync();

            if (empresa.PlanoId == null)
            {
                empresa.PlanoId = plano.Id;
                await _context.SaveChangesAsync();
            }

            var priceId = tipoPlano == "anual"
                ? plano.StripePriceIdAnual
                : plano.StripePriceIdMensal;

            if (string.IsNullOrEmpty(priceId))
            {
                throw new InvalidOperationException("Plano sem preço configurado no Stripe.");
            }

            var customerId = await ObterOuCriarStripeCustomerIdAsync(empresa);

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                // O Stripe descontinuou "embedded" em favor de "embedded_page"
                // (o valor antigo agora retorna invalid_request_error).
                UiMode = "embedded_page",
                Customer = customerId,
                ClientReferenceId = empresaId.ToString(),
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions { Price = priceId, Quantity = 1 }
                },
                ReturnUrl = urlRetorno,
                AllowPromotionCodes = false
            };

            // Promoção dos 3 primeiros meses só faz sentido no plano mensal.
            if (tipoPlano != "anual" && !string.IsNullOrEmpty(plano.StripeCouponIdPromocional))
            {
                options.Discounts = new List<SessionDiscountOptions>
                {
                    new SessionDiscountOptions { Coupon = plano.StripeCouponIdPromocional }
                };
            }

            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(options);

            return session.ClientSecret;
        }

        public async Task<string> ObterStatusCheckoutAsync(string sessionId)
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(sessionId);

            return session.Status;
        }

        public async Task<string> CriarPortalSessionAsync(int empresaId, string urlRetorno)
        {
            var empresa = await _context.Empresas
                .FirstOrDefaultAsync(e => e.Id == empresaId);

            if (empresa == null || string.IsNullOrEmpty(empresa.StripeCustomerId))
            {
                throw new InvalidOperationException("Empresa ainda não possui assinatura no Stripe.");
            }

            // Sincroniza telefone/endereço/e-mail atuais da empresa com o
            // Customer antes de abrir o portal — sem isso, quem preencheu
            // esses dados depois de já ter assinado nunca via isso lá.
            await ObterOuCriarStripeCustomerIdAsync(empresa);

            var portalService = new Stripe.BillingPortal.SessionService();

            var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = empresa.StripeCustomerId,
                ReturnUrl = urlRetorno
            });

            return session.Url;
        }

        public async Task ProcessarEventoAsync(Event stripeEvent)
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                {
                    var session = stripeEvent.Data.Object as Session;

                    if (session?.ClientReferenceId != null &&
                        int.TryParse(session.ClientReferenceId, out var empresaId))
                    {
                        var empresa = await _context.Empresas.FindAsync(empresaId);

                        if (empresa != null)
                        {
                            empresa.StripeCustomerId = session.CustomerId ?? empresa.StripeCustomerId;
                            empresa.StripeSubscriptionId = session.SubscriptionId;
                            empresa.AssinaturaStatus = "active";

                            await _context.SaveChangesAsync();
                        }
                    }

                    break;
                }

                case "customer.subscription.created":
                case "customer.subscription.updated":
                {
                    var subscription = stripeEvent.Data.Object as Subscription;

                    if (subscription != null)
                    {
                        var empresa = await _context.Empresas.FirstOrDefaultAsync(e =>
                            e.StripeSubscriptionId == subscription.Id ||
                            e.StripeCustomerId == subscription.CustomerId);

                        if (empresa != null)
                        {
                            empresa.StripeSubscriptionId = subscription.Id;
                            empresa.AssinaturaStatus = subscription.Status;

                            var periodoAtual = subscription.Items?.Data?
                                .Select(i => i.CurrentPeriodEnd)
                                .OrderByDescending(d => d)
                                .FirstOrDefault();

                            if (periodoAtual.HasValue)
                            {
                                empresa.AssinaturaValidaAte = periodoAtual.Value;
                            }

                            await _context.SaveChangesAsync();
                        }
                    }

                    break;
                }

                case "customer.subscription.deleted":
                {
                    var subscription = stripeEvent.Data.Object as Subscription;

                    if (subscription != null)
                    {
                        var empresa = await _context.Empresas.FirstOrDefaultAsync(e =>
                            e.StripeSubscriptionId == subscription.Id);

                        if (empresa != null)
                        {
                            empresa.AssinaturaStatus = "canceled";
                            await _context.SaveChangesAsync();
                        }
                    }

                    break;
                }
            }
        }
    }
}
