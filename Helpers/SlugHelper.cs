using System.Globalization;
using System.Text;

namespace EmpresaAgendamento.Helpers
{
    public static class SlugHelper
    {
        // Rotas de um segmento só na raiz do site (PublicoController usa
        // [HttpGet("{slug}")] sob [Route("")]) — uma empresa não pode usar
        // nenhuma delas como slug, senão a rota literal sempre vence e a
        // página dela fica inacessível.
        private static readonly HashSet<string> Reservados = new(StringComparer.OrdinalIgnoreCase)
        {
            "login", "empresas", "termos-de-uso", "politica-de-privacidade",
            "acesso-negado", "sessao-expirada", "erro", "manutencao",
            "css", "js", "lib", "img", "uploads"
        };

        public static bool EhReservado(string? slug) =>
            !string.IsNullOrWhiteSpace(slug) && Reservados.Contains(slug.Trim());

        // "Salão Beleza & Cia" -> "salao-beleza-cia"
        public static string Gerar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "";

            var semAcento = RemoverAcentos(texto.ToLowerInvariant());

            var chars = semAcento
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();

            var slug = new string(chars);

            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");

            return slug.Trim('-');
        }

        private static string RemoverAcentos(string texto)
        {
            var normalizado = texto.Normalize(NormalizationForm.FormD);
            var semAcento = normalizado.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(semAcento.ToArray()).Normalize(NormalizationForm.FormC);
        }
    }
}
