namespace EmpresaAgendamento.Services
{
    public interface INotificacaoAgendamentoService
    {
        // Manda e-mail de confirmação pro cliente do agendamento, se ele
        // tiver e-mail cadastrado (cliente avulso não tem). Nunca lança —
        // uma falha de e-mail não pode travar o fluxo de agendamento.
        Task EnviarConfirmacaoAsync(int agendamentoId);
    }
}
