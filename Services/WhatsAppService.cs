using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EmpresaAgendamento.Services
{
    // WhatsApp Cloud API (Meta) — https://developers.facebook.com/docs/whatsapp/cloud-api.
    // Modo atual: mensagem de texto livre. Isso só é aceito pela Meta em duas
    // situações: (1) número de TESTE do painel Meta for Developers, mandando
    // pros até 5 números verificados; ou (2) dentro da janela de 24h depois
    // do cliente ter mandado mensagem pra esse número antes. Um lembrete de
    // verdade (a empresa manda primeiro, sem o cliente ter escrito) exige um
    // "message template" pré-aprovado pela Meta — quando isso for criado e
    // aprovado no Business Manager, troca o payload de "text" pra "template"
    // aqui, só nesse método (o resto do pipeline não muda).
    //
    // Configuração (User Secrets / variável de ambiente, nunca appsettings.json):
    //   WhatsApp:CloudApi:PhoneNumberId  — o "Phone number ID" do painel da Meta
    //   WhatsApp:CloudApi:AccessToken    — o token de acesso (temporário no modo teste)
    //   WhatsApp:CloudApi:ApiVersion     — opcional, padrão "v21.0"
    // Sem essas chaves configuradas, cai de volta pro comportamento antigo
    // (só grava no log) — não trava o app enquanto não estiver pronto.
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(HttpClient httpClient, IConfiguration configuration, ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarMensagemAsync(string telefoneDestino, string mensagem)
        {
            var phoneNumberId = _configuration["WhatsApp:CloudApi:PhoneNumberId"];
            var accessToken = _configuration["WhatsApp:CloudApi:AccessToken"];

            if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogInformation(
                    "[WhatsApp - Cloud API não configurada] Para {Telefone}: {Mensagem}",
                    telefoneDestino,
                    mensagem);

                return;
            }

            var numero = NormalizarNumero(telefoneDestino);

            if (numero == null)
            {
                _logger.LogWarning("Telefone inválido pra WhatsApp: {Telefone}", telefoneDestino);
                return;
            }

            var apiVersion = _configuration["WhatsApp:CloudApi:ApiVersion"];
            apiVersion = string.IsNullOrWhiteSpace(apiVersion) ? "v21.0" : apiVersion;

            var payload = new
            {
                messaging_product = "whatsapp",
                to = numero,
                type = "text",
                text = new { body = mensagem }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var corpo = await response.Content.ReadAsStringAsync();

                    _logger.LogWarning(
                        "Falha ao enviar WhatsApp pra {Telefone}: {Status} — {Corpo}",
                        telefoneDestino,
                        response.StatusCode,
                        corpo);
                }
            }
            catch (Exception ex)
            {
                // Nunca deixa um problema no WhatsApp derrubar o lembrete/fluxo
                // que chamou — é sempre um efeito colateral, nunca crítico.
                _logger.LogError(ex, "Erro ao chamar o WhatsApp Cloud API pra {Telefone}.", telefoneDestino);
            }
        }

        // A Cloud API espera o número em E.164 sem "+" (ex.: 5511987654321).
        // Números salvos aqui vêm formatados ("(11) 98765-4321") e sem DDI —
        // limpa a formatação e assume Brasil (55) quando não tiver DDI já.
        private static string? NormalizarNumero(string telefone)
        {
            var digitos = new string((telefone ?? "").Where(char.IsDigit).ToArray());

            if (digitos.Length < 10)
                return null;

            return digitos.Length <= 11 ? "55" + digitos : digitos;
        }
    }
}
