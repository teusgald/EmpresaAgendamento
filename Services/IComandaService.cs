namespace EmpresaAgendamento.Services
{
    public interface IComandaService
    {
        // Adiciona um produto à comanda do agendamento — baixa o estoque e
        // atualiza o valor previsto da conta a receber. empresaId é sempre
        // validado contra o dono real do agendamento/produto antes de agir.
        Task<(bool Sucesso, string? Erro)> AdicionarItemAsync(
            int empresaId, int agendamentoId, int produtoId, int quantidade);

        // Remove um item da comanda e devolve a quantidade ao estoque.
        Task<(bool Sucesso, string? Erro)> RemoverItemAsync(int empresaId, int itemComandaId);
    }
}
