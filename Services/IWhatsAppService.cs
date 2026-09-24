namespace EmpresaAgendamento.Services
{
    public interface IWhatsAppService
    {
        // Texto livre — só é aceito pela Meta em modo de teste ou dentro da
        // janela de 24h após o cliente ter escrito. `phoneNumberIdEmpresa`
        // nulo cai pro número global (WhatsApp:CloudApi:PhoneNumberId).
        Task EnviarMensagemAsync(string telefoneDestino, string mensagem, string? phoneNumberIdEmpresa = null);

        // Message template pré-aprovado no Meta Business Manager — único jeito
        // válido de a empresa iniciar a conversa (confirmação, lembrete).
        // `phoneNumberIdEmpresa` nulo cai pro número global.
        Task EnviarTemplateAsync(
            string telefoneDestino,
            string? phoneNumberIdEmpresa,
            string templateName,
            string idioma,
            IReadOnlyList<string> parametros);
    }
}
