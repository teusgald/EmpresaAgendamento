namespace EmpresaAgendamento.Services
{
    public interface IAiAssistantService
    {
        // Nunca lança — qualquer falha (não configurado, erro de rede, limite
        // da API) volta como uma mensagem amigável de assistente, igual ao
        // padrão defensivo do WhatsAppService.
        Task<string> ConversarAsync(
            int empresaId,
            string mensagemUsuario,
            IReadOnlyList<(string Role, string Mensagem)> historico);
    }
}
