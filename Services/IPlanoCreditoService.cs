namespace EmpresaAgendamento.Services
{
    public interface IPlanoCreditoService
    {
        // Se o cliente tiver um plano ativo que cobre esse serviço e ainda
        // tiver crédito no período, debita 1 e retorna o Id da assinatura
        // usada (pra o agendamento guardar e poder devolver se cancelar).
        // Retorna null se não há plano aplicável ou não há crédito.
        Task<int?> ConsumirSeAplicavelAsync(int empresaId, int clienteId, int servicoId);

        // Devolve 1 crédito pra assinatura (usado quando o agendamento que
        // consumiu é cancelado). Nunca lança.
        Task DevolverCreditoAsync(int assinaturaPlanoServicoId);
    }
}
