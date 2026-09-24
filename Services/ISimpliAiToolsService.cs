namespace EmpresaAgendamento.Services
{
    // Ferramentas somente-leitura que o Simpli AI pode chamar. Todo método
    // recebe empresaId já resolvido pelo controller (nunca aceita esse valor
    // vindo de argumento do modelo) e escopa a consulta por ele, do mesmo
    // jeito que o resto do sistema (AgendamentosController, FinanceiroController).
    public interface ISimpliAiToolsService
    {
        Task<object> ObterResumoAgendamentosAsync(int empresaId, DateTime inicio, DateTime fim);

        Task<object> ObterResumoFinanceiroAsync(int empresaId, DateTime inicio, DateTime fim);
    }
}
