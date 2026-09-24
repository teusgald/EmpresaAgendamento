namespace EmpresaAgendamento.Services
{
    public interface INotificacaoAgendamentoService
    {
        // Manda confirmação pro cliente do agendamento — e-mail (se tiver
        // cadastrado; cliente avulso não tem) e WhatsApp (se o plano da
        // empresa permitir). Nunca lança — uma falha num canal não pode
        // travar o fluxo de agendamento nem impedir o outro canal.
        Task EnviarConfirmacaoAsync(int agendamentoId);
    }
}
