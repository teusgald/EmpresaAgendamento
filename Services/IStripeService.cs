namespace EmpresaAgendamento.Services
{
    public interface IStripeService
    {
        // Idempotente: cria o Produto/Preços/Cupom no Stripe (e o Plano local)
        // na primeira vez; nas próximas, só retorna o Plano já existente.
        Task<Models.Plano> GarantirPlanoPadraoAsync();

        // tipoPlano: "mensal" ou "anual". Retorna o client_secret da Checkout
        // Session (ui_mode=embedded) pra montar o formulário de pagamento
        // dentro da própria página, sem redirecionar pro site do Stripe.
        Task<string> CriarCheckoutClientSecretAsync(int empresaId, string tipoPlano, string urlRetorno);

        // Consulta o status ("open", "complete", "expired") de uma Checkout
        // Session pelo id — usado quando o Stripe volta pro return_url.
        Task<string> ObterStatusCheckoutAsync(string sessionId);

        // URL do portal de faturamento hospedado pelo Stripe (cancelar,
        // trocar cartão, ver faturas) — não precisamos construir nada disso.
        Task<string> CriarPortalSessionAsync(int empresaId, string urlRetorno);

        // Processa um evento de webhook do Stripe já validado (assinatura
        // conferida antes de chamar isso) e sincroniza o status da empresa.
        Task ProcessarEventoAsync(Stripe.Event stripeEvent);
    }
}
