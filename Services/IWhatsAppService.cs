namespace EmpresaAgendamento.Services
{
    public interface IWhatsAppService
    {
        Task EnviarMensagemAsync(string telefoneDestino, string mensagem);
    }
}
