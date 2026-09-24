using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmpresaAgendamento.Services
{
    // Gemini API (Google) — https://ai.google.dev/api/generate-content.
    // Tier gratuito por enquanto (ver Empresa.SimpliAiBetaAtivo) — nesse tier,
    // segundo os termos do Google, prompt/resposta podem ser usados pra
    // melhorar o produto deles, por isso só empresa de teste usa isso hoje.
    //
    // Configuração (User Secrets / variável de ambiente, nunca appsettings.json):
    //   AiAssistant:Gemini:ApiKey  — chave do Google AI Studio
    //   AiAssistant:Gemini:Model   — opcional, padrão "gemini-3.6-flash"
    // Sem a chave configurada, devolve uma mensagem amigável em vez de tentar
    // chamar a API — nunca derruba o chat.
    public class GeminiAssistantService : IAiAssistantService
    {
        private const int MaxIteracoesFerramentas = 4;

        private const string InstrucaoSistema =
            "Você é o Simpli AI, assistente do sistema Simpli Time (agendamento pra " +
            "salão, clínica, petshop e afins). Responda sempre em português do Brasil, " +
            "de forma direta e prática. Quando a pergunta envolver número real da empresa " +
            "(agendamentos, faturamento, contas), use as ferramentas disponíveis em vez de " +
            "chutar — nunca invente número. Se a ferramenta não trouxer dado nenhum, diga " +
            "isso claramente em vez de inventar. Você ainda NÃO consegue gerar imagem — se " +
            "pedirem imagem pra post, diga isso com clareza (sem fingir que gerou) e ofereça " +
            "escrever a legenda/texto do post em vez disso.";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ISimpliAiToolsService _tools;
        private readonly ILogger<GeminiAssistantService> _logger;

        public GeminiAssistantService(
            HttpClient httpClient,
            IConfiguration configuration,
            ISimpliAiToolsService tools,
            ILogger<GeminiAssistantService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _tools = tools;
            _logger = logger;
        }

        public async Task<string> ConversarAsync(
            int empresaId,
            string mensagemUsuario,
            IReadOnlyList<(string Role, string Mensagem)> historico)
        {
            var apiKey = _configuration["AiAssistant:Gemini:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogInformation("[Simpli AI - Gemini não configurado] Empresa {EmpresaId}", empresaId);
                return "O Simpli AI ainda não está configurado nesta instalação.";
            }

            var model = _configuration["AiAssistant:Gemini:Model"];
            model = string.IsNullOrWhiteSpace(model) ? "gemini-3.6-flash" : model;

            var contents = new JsonArray();

            foreach (var (role, mensagem) in historico)
            {
                contents.Add(MontarConteudoTexto(role, mensagem));
            }

            contents.Add(MontarConteudoTexto("user", mensagemUsuario));

            try
            {
                for (var iteracao = 0; iteracao < MaxIteracoesFerramentas; iteracao++)
                {
                    var payload = new JsonObject
                    {
                        ["systemInstruction"] = new JsonObject
                        {
                            ["parts"] = new JsonArray { new JsonObject { ["text"] = InstrucaoSistema } }
                        },
                        ["contents"] = DeepClone(contents),
                        ["tools"] = new JsonArray { MontarDeclaracaoFerramentas() }
                    };

                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}")
                    {
                        Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
                    };

                    var response = await _httpClient.SendAsync(request);
                    var corpo = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Falha ao chamar o Gemini (empresa {EmpresaId}): {Status} — {Corpo}",
                            empresaId, response.StatusCode, corpo);

                        return "Não consegui falar com o Simpli AI agora. Tente de novo em instantes.";
                    }

                    using var documento = JsonDocument.Parse(corpo);

                    var candidato = documento.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content");

                    var partes = candidato.GetProperty("parts");

                    var chamadaFuncao = partes.EnumerateArray()
                        .FirstOrDefault(p => p.TryGetProperty("functionCall", out _));

                    if (chamadaFuncao.ValueKind == JsonValueKind.Undefined)
                    {
                        var texto = string.Concat(partes.EnumerateArray()
                            .Where(p => p.TryGetProperty("text", out _))
                            .Select(p => p.GetProperty("text").GetString()));

                        return string.IsNullOrWhiteSpace(texto)
                            ? "Não entendi bem — pode reformular a pergunta?"
                            : texto;
                    }

                    // Reconstrói o turno do modelo (a chamada de função) e devolve
                    // o resultado da ferramenta, exatamente como a Gemini API espera
                    // pra continuar a conversa.
                    contents.Add(JsonNode.Parse(candidato.GetRawText()));

                    var funcao = chamadaFuncao.GetProperty("functionCall");
                    var nomeFuncao = funcao.GetProperty("name").GetString() ?? "";
                    var argumentos = funcao.TryGetProperty("args", out var argsEl) ? argsEl : default;

                    var resultado = await ExecutarFerramentaAsync(empresaId, nomeFuncao, argumentos);

                    contents.Add(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["functionResponse"] = new JsonObject
                                {
                                    ["name"] = nomeFuncao,
                                    ["response"] = JsonNode.Parse(JsonSerializer.Serialize(resultado))
                                }
                            }
                        }
                    });
                }

                return "Sua pergunta ficou complexa demais pro Simpli AI agora — tenta perguntar de um jeito mais direto?";
            }
            catch (Exception ex)
            {
                // Nunca deixa um problema no Simpli AI derrubar a tela do chat —
                // é sempre um efeito colateral, nunca crítico.
                _logger.LogError(ex, "Erro ao conversar com o Simpli AI (empresa {EmpresaId}).", empresaId);
                return "Não consegui processar sua pergunta agora. Tente de novo em instantes.";
            }
        }

        private async Task<object> ExecutarFerramentaAsync(int empresaId, string nomeFuncao, JsonElement argumentos)
        {
            DateTime inicio = DateTime.Today.AddDays(-30);
            DateTime fim = DateTime.Today;

            if (argumentos.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    if (argumentos.TryGetProperty("dataInicial", out var i) && DateTime.TryParse(i.GetString(), out var di))
                        inicio = di;

                    if (argumentos.TryGetProperty("dataFinal", out var f) && DateTime.TryParse(f.GetString(), out var df))
                        fim = df;
                }
                catch
                {
                    // mantém o período default de 30 dias se o argumento vier malformado.
                }
            }

            return nomeFuncao switch
            {
                "consultar_resumo_agendamentos" => await _tools.ObterResumoAgendamentosAsync(empresaId, inicio, fim),
                "consultar_resumo_financeiro" => await _tools.ObterResumoFinanceiroAsync(empresaId, inicio, fim),
                _ => new { erro = "Ferramenta desconhecida." }
            };
        }

        private static JsonObject MontarConteudoTexto(string role, string texto) => new()
        {
            ["role"] = role == "model" ? "model" : "user",
            ["parts"] = new JsonArray { new JsonObject { ["text"] = texto } }
        };

        private static JsonObject MontarDeclaracaoFerramentas() => new()
        {
            ["functionDeclarations"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "consultar_resumo_agendamentos",
                    ["description"] = "Retorna total de agendamentos, faturamento de serviços finalizados e serviços mais agendados da empresa, num período.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["dataInicial"] = new JsonObject { ["type"] = "string", ["description"] = "Data inicial no formato yyyy-MM-dd" },
                            ["dataFinal"] = new JsonObject { ["type"] = "string", ["description"] = "Data final no formato yyyy-MM-dd" }
                        },
                        ["required"] = new JsonArray { "dataInicial", "dataFinal" }
                    }
                },
                new JsonObject
                {
                    ["name"] = "consultar_resumo_financeiro",
                    ["description"] = "Retorna receitas, despesas, lucro e contas a receber/pagar em aberto da empresa, num período.",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["dataInicial"] = new JsonObject { ["type"] = "string", ["description"] = "Data inicial no formato yyyy-MM-dd" },
                            ["dataFinal"] = new JsonObject { ["type"] = "string", ["description"] = "Data final no formato yyyy-MM-dd" }
                        },
                        ["required"] = new JsonArray { "dataInicial", "dataFinal" }
                    }
                }
            }
        };

        private static JsonNode? DeepClone(JsonNode node) => JsonNode.Parse(node.ToJsonString());
    }
}
