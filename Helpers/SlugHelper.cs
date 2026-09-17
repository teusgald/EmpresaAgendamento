using System.Globalization;
using System.Text;

namespace EmpresaAgendamento.Helpers
{
    public static class SlugHelper
    {
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
