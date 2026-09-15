using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Services
{
    public interface IFinanceiroService
    {
        // =========================
        // INTEGRAÇÃO COM AGENDAMENTO
        // =========================

        // Idempotente: um agendamento nunca gera mais de uma conta a receber
        // (retorna a existente se já houver uma). Chamado assim que o
        // agendamento é criado (Agendado/Confirmado já contam como previsão
        // de receita) — e de novo, com segurança, ao finalizar, para cobrir
        // agendamentos antigos que nunca passaram por aqui.
        Task<ContaReceber?> GerarContaReceberDeAgendamentoAsync(int agendamentoId);

        // Calcula e registra a comissão do funcionário responsável (idempotente).
        // Só deve ser chamado quando o agendamento é Finalizado — a comissão é
        // sobre serviço efetivamente realizado, não sobre o agendado.
        Task ApurarComissaoDoAgendamentoAsync(int agendamentoId);

        // Cancela/estorna a conta a receber vinculada ao agendamento, se existir.
        Task CancelarContaReceberDeAgendamentoAsync(int agendamentoId, string motivo);

        // =========================
        // RECEBIMENTOS
        // =========================

        // Registra recebimento total ou parcial. empresaId é sempre validado
        // contra o dono real da conta antes de qualquer alteração.
        Task<(bool Sucesso, string? Erro, ContaReceber? Conta)> RegistrarRecebimentoAsync(
            int contaReceberId,
            int empresaId,
            decimal valor,
            FormaPagamento formaPagamento,
            string? usuarioId,
            DateTime? dataRecebimento = null);

        // Cancela ou estorna (se já houve recebimento confirmado) uma conta a
        // receber. Nunca exclui: preserva histórico via Status/MovimentacaoFinanceira.
        Task<(bool Sucesso, string? Erro)> CancelarContaReceberAsync(
            int contaReceberId,
            int empresaId,
            string motivo);

        // =========================
        // PAGAMENTOS (DESPESAS)
        // =========================

        // Registra pagamento total ou parcial de uma conta a pagar. empresaId
        // é sempre validado contra o dono real da conta antes de qualquer alteração.
        Task<(bool Sucesso, string? Erro, ContaPagar? Conta)> RegistrarPagamentoAsync(
            int contaPagarId,
            int empresaId,
            decimal valor,
            FormaPagamento formaPagamento,
            string? usuarioId,
            DateTime? dataPagamento = null);

        // Cancela ou estorna (se já houve pagamento confirmado) uma conta a
        // pagar. Nunca exclui: preserva histórico via Status/MovimentacaoFinanceira.
        Task<(bool Sucesso, string? Erro)> CancelarContaPagarAsync(
            int contaPagarId,
            int empresaId,
            string motivo);

        // =========================
        // COMISSÕES
        // =========================

        // =========================
        // SINCRONIZAÇÃO (BACKFILL)
        // =========================

        // Gera a conta a receber (e cancela/apura comissão conforme o status
        // atual) de todo agendamento da empresa que ainda não tem uma —
        // cobre agendamentos criados antes da integração com o financeiro
        // existir. Idempotente: pode ser rodado quantas vezes precisar.
        // Retorna quantos agendamentos foram sincronizados agora.
        Task<int> SincronizarAgendamentosExistentesAsync(int empresaId);

        // Paga de uma vez todas as comissões pendentes (AgendamentoFuncionario
        // com ValorComissao apurado e ValorRecebido nulo) de um funcionário,
        // dentro do período informado. Gera a ContaPagar (categoria "Comissões")
        // já quitada + a movimentação de saída, e marca cada comissão como paga.
        Task<(bool Sucesso, string? Erro, ContaPagar? Conta)> PagarComissoesPendentesAsync(
            int empresaId,
            int funcionarioId,
            DateTime? dataInicial,
            DateTime? dataFinal,
            FormaPagamento formaPagamento,
            string? usuarioId);
    }
}
