using Microsoft.AspNetCore.Http;

namespace EmpresaAgendamento.Helpers
{
    // Monta a base (scheme+host) dos links mandados por e-mail (confirmação,
    // reset de senha, definir senha). Em produção, usa "App:BaseUrl" da config
    // em vez do Host da requisição — um Host forjado (se o proxy/App Service
    // não validar isso) não pode desviar o link de reset pra outro domínio.
    // Sem "App:BaseUrl" configurado (dev local), cai no Host da requisição
    // mesmo, pra não exigir configuração extra em ambiente local.
    public static class LinkBaseHelper
    {
        public static string ObterBase(HttpRequest request, IConfiguration configuration)
        {
            var baseConfigurada = configuration["App:BaseUrl"];

            return string.IsNullOrWhiteSpace(baseConfigurada)
                ? $"{request.Scheme}://{request.Host}"
                : baseConfigurada.TrimEnd('/');
        }
    }
}
