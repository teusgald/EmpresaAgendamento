namespace EmpresaAgendamento.Services
{
    public interface IStripeService
    {
        // Idempotente: cria os 3 tiers (Start/Pro/Business) — Produto/Preços
        // no Stripe (e o Plano local) — na primeira vez; nas próximas, só
        // retorna os Planos já existentes. Ordenados por ValorMensal.
        Task<List<Models.Plano>> GarantirPlanosAsync();

        // tipoPlano: "mensal", "semestral" ou "anual". Retorna o client_secret
        // da Checkout Session (ui_mode=embedded_page) pra montar o formulário
        // de pagamento dentro da própria página, sem redirecionar pro site do
        // Stripe. Troca o Plano da empresa para planoId se for diferente do
        // atual (upgrade/downgrade).
        Task<string> CriarCheckoutClientSecretAsync(int empresaId, int planoId, string tipoPlano, string urlRetorno);

        // Consulta o status ("open", "complete", "expired") de uma Checkout
        // Session pelo id — usado quando o Stripe volta pro return_url.
        Task<string> ObterStatusCheckoutAsync(string sessionId);

        // URL do portal de faturamento hospedado pelo Stripe (cancelar,
        // trocar cartão, ver faturas) — não precisamos construir nada disso.
        Task<string> CriarPortalSessionAsync(int empresaId, string urlRetorno);

        // Processa um evento de webhook do Stripe já validado (assinatura
        // conferida antes de chamar isso) e sincroniza o status da empresa.
        Task ProcessarEventoAsync(Stripe.Event stripeEvent);

        // Quantidade e soma (em R$) das faturas pagas no Stripe dentro do
        // intervalo [inicio, fimExclusivo) — usado no Faturamento do painel
        // do dono do sistema.
        Task<(int TotalFaturas, decimal ValorTotal)> ListarFaturasPagasAsync(DateTime inicio, DateTime fimExclusivo);
    }
}
