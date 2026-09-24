using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EmpresaAgendamento.Services
{
    // WhatsApp Cloud API (Meta) — https://developers.facebook.com/docs/whatsapp/cloud-api.
    // Uma WABA (WhatsApp Business Account) só, com um token de acesso só —
    // cada empresa tem seu próprio "Phone number ID", registrado nessa mesma
    // WABA, então os templates aprovados valem pra todos os números.
    //
    // Duas formas de mandar mensagem:
    //   - EnviarMensagemAsync: texto livre. Só aceito pela Meta em modo TESTE
    //     do painel (até 5 números verificados) ou dentro da janela de 24h
    //     depois do cliente ter escrito primeiro.
    //   - EnviarTemplateAsync: "message template" pré-aprovado no Business
    //     Manager — único jeito válido de a empresa iniciar a conversa
    //     (confirmação de agendamento, lembrete).
    //
    // Configuração (User Secrets / variável de ambiente, nunca appsettings.json):
    //   WhatsApp:CloudApi:PhoneNumberId  — número global (fallback quando a
    //                                      empresa ainda não tem o dela)
    //   WhatsApp:CloudApi:AccessToken    — token da WABA, vale pra todo número
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

        public Task EnviarMensagemAsync(string telefoneDestino, string mensagem, string? phoneNumberIdEmpresa = null)
        {
            return EnviarAsync(telefoneDestino, phoneNumberIdEmpresa, numero => new
            {
                messaging_product = "whatsapp",
                to = numero,
                type = "text",
                text = new { body = mensagem }
            });
        }

        public Task EnviarTemplateAsync(
            string telefoneDestino,
            string? phoneNumberIdEmpresa,
            string templateName,
            string idioma,
            IReadOnlyList<string> parametros)
        {
            return EnviarAsync(telefoneDestino, phoneNumberIdEmpresa, numero => new
            {
                messaging_product = "whatsapp",
                to = numero,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = idioma },
                    components = new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters = parametros.Select(p => new { type = "text", text = p }).ToArray()
                        }
                    }
                }
            });
        }

        private async Task EnviarAsync(string telefoneDestino, string? phoneNumberIdEmpresa, Func<string, object> montarPayload)
        {
            var phoneNumberId = phoneNumberIdEmpresa ?? _configuration["WhatsApp:CloudApi:PhoneNumberId"];
            var accessToken = _configuration["WhatsApp:CloudApi:AccessToken"];

            if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogInformation(
                    "[WhatsApp - Cloud API não configurada] Para {Telefone}",
                    telefoneDestino);

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

            var payload = montarPayload(numero);

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
