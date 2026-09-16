namespace EmpresaAgendamento.Services
{
    // Ainda sem provedor de WhatsApp contratado (Z-API, Twilio ou Meta Cloud
    // API — a decidir com o dono do produto). Por enquanto só registra no log
    // pra não travar o pipeline de lembretes; quando o provedor for escolhido,
    // troca só esta implementação (chamada HTTP pra API do provedor) — o resto
    // do fluxo (agendamento de envio, controle de duplicidade) já está pronto.
    public class WhatsAppService : IWhatsAppService
    {
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(ILogger<WhatsAppService> logger)
        {
            _logger = logger;
        }

        public Task EnviarMensagemAsync(string telefoneDestino, string mensagem)
        {
            _logger.LogInformation(
                "[WhatsApp - provedor não configurado] Para {Telefone}: {Mensagem}",
                telefoneDestino,
                mensagem);

            return Task.CompletedTask;
        }
    }
}
