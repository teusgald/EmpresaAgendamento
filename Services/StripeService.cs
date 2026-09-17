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

        private const string Moeda = "brl";
        private const string NomePlanoLegado = "Plano Padrão";

        // Preços de mercado definidos com o dono do produto — 3 tiers por
        // número de profissionais. A promoção de R$9,99 nos 3 primeiros
        // meses (cupom) só existe no Start, que é o plano de entrada.
        private static readonly PlanoSeed[] PlanosSeed =
        {
            new("Start", LimiteFuncionarios: 2, ValorMensal: 39.90m, ValorSemestral: 269.00m, ValorAnual: 499.00m, ValorMensalPromocional: 9.99m),
            new("Pro", LimiteFuncionarios: 5, ValorMensal: 79.90m, ValorSemestral: 429.00m, ValorAnual: 799.00m, ValorMensalPromocional: null),
            new("Business", LimiteFuncionarios: 0, ValorMensal: 119.90m, ValorSemestral: 649.00m, ValorAnual: 1199.00m, ValorMensalPromocional: null)
        };

        private record PlanoSeed(
            string Nome, int LimiteFuncionarios, decimal ValorMensal,
            decimal ValorSemestral, decimal ValorAnual, decimal? ValorMensalPromocional);

        public StripeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Plano>> GarantirPlanosAsync()
        {
            await MigrarPlanoLegadoAsync();

            var planos = new List<Plano>();

            foreach (var seed in PlanosSeed)
            {
                planos.Add(await GarantirPlanoAsync(seed));
            }

            return planos.OrderBy(p => p.ValorMensal).ToList();
        }

        // O sistema começou com um único "Plano Padrão" pra qualquer empresa.
        // Com os 3 tiers, quem já estava nesse plano legado é migrado pro Pro
        // (mais próximo do que ele tinha: todos os módulos liberados) — assim
        // ninguém perde acesso quando os planos novos entram no ar.
        private async Task MigrarPlanoLegadoAsync()
        {
            var planoLegado = await _context.Planos
                .Include(p => p.Empresas)
                .FirstOrDefaultAsync(p => p.Nome == NomePlanoLegado);

            if (planoLegado == null || !planoLegado.Empresas.Any())
                return;

            var planoPro = await GarantirPlanoAsync(PlanosSeed.First(s => s.Nome == "Pro"));

            foreach (var empresa in planoLegado.Empresas.ToList())
            {
                empresa.PlanoId = planoPro.Id;
            }

            planoLegado.Ativo = false;
            await _context.SaveChangesAsync();
        }

        private async Task<Plano> GarantirPlanoAsync(PlanoSeed seed)
        {
            var plano = await _context.Planos.FirstOrDefaultAsync(p => p.Nome == seed.Nome);

            if (plano != null &&
                !string.IsNullOrEmpty(plano.StripePriceIdMensal) &&
                !string.IsNullOrEmpty(plano.StripePriceIdSemestral) &&
                !string.IsNullOrEmpty(plano.StripePriceIdAnual) &&
                (seed.ValorMensalPromocional == null || !string.IsNullOrEmpty(plano.StripeCouponIdPromocional)))
            {
                return plano;
            }

            var productService = new ProductService();
            var product = await productService.CreateAsync(new ProductCreateOptions
            {
                Name = $"Simpli Time — {seed.Nome}",
                Description = $"Assinatura do sistema de agendamentos Simpli Time — plano {seed.Nome}."
            });

            var priceService = new PriceService();

            var priceMensal = await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                Currency = Moeda,
                UnitAmount = (long)(seed.ValorMensal * 100),
                Recurring = new PriceRecurringOptions { Interval = "month" }
            });

            var priceSemestral = await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                Currency = Moeda,
                UnitAmount = (long)(seed.ValorSemestral * 100),
                Recurring = new PriceRecurringOptions { Interval = "month", IntervalCount = 6 }
            });

            var priceAnual = await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                Currency = Moeda,
                UnitAmount = (long)(seed.ValorAnual * 100),
                Recurring = new PriceRecurringOptions { Interval = "year" }
            });

            string? cupomId = null;

            if (seed.ValorMensalPromocional.HasValue)
            {
                var couponService = new CouponService();

                var cupom = await couponService.CreateAsync(new CouponCreateOptions
                {
                    Name = $"Promoção 3 primeiros meses — {seed.Nome}",
                    Currency = Moeda,
                    AmountOff = (long)((seed.ValorMensal - seed.ValorMensalPromocional.Value) * 100),
                    Duration = "repeating",
                    DurationInMonths = 3
                });

                cupomId = cupom.Id;
            }

            if (plano == null)
            {
                plano = new Plano
                {
                    Nome = seed.Nome,
                    ValorMensal = seed.ValorMensal,
                    ValorSemestral = seed.ValorSemestral,
                    ValorAnual = seed.ValorAnual,
                    LimiteFuncionarios = seed.LimiteFuncionarios,
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
            plano.StripePriceIdSemestral = priceSemestral.Id;
            plano.StripePriceIdAnual = priceAnual.Id;

            if (cupomId != null)
            {
                plano.StripeCouponIdPromocional = cupomId;
            }

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
            int empresaId, int planoId, string tipoPlano, string urlRetorno)
        {
            var empresa = await _context.Empresas
                .FirstOrDefaultAsync(e => e.Id == empresaId);

            if (empresa == null)
            {
                throw new InvalidOperationException("Empresa não encontrada.");
            }

            var plano = await _context.Planos.FirstOrDefaultAsync(p => p.Id == planoId);

            if (plano == null)
            {
                throw new InvalidOperationException("Plano não encontrado.");
            }

            // Guarda o plano escolhido mesmo se a empresa estiver trocando de
            // tier (upgrade/downgrade) — o Stripe é quem manda no valor
            // cobrado de fato a partir do priceId abaixo.
            if (empresa.PlanoId != plano.Id)
            {
                empresa.PlanoId = plano.Id;
                await _context.SaveChangesAsync();
            }

            var priceId = tipoPlano switch
            {
                "anual" => plano.StripePriceIdAnual,
                "semestral" => plano.StripePriceIdSemestral,
                _ => plano.StripePriceIdMensal
            };

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

            // Promoção dos 3 primeiros meses só faz sentido na cobrança
            // mensal (e só existe no plano Start).
            if (tipoPlano == "mensal" && !string.IsNullOrEmpty(plano.StripeCouponIdPromocional))
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
