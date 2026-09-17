using System.Globalization;
using System.Text.RegularExpressions;

namespace EmpresaAgendamento.Services.Ofx
{
    // Parser mínimo de extrato OFX (bancos BR exportam quase sempre OFX 1.x —
    // SGML, sem fechar toda tag folha), sem depender de biblioteca externa:
    // cada <STMTTRN>...</STMTTRN> é um lançamento, e dentro dele procuramos
    // as tags por regex em vez de exigir XML bem-formado.
    public static class OfxParser
    {
        private static readonly Regex BlocoTransacaoRegex = new(
            "<STMTTRN>(.*?)</STMTTRN>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<OfxTransacao> Parse(string conteudo)
        {
            var transacoes = new List<OfxTransacao>();

            foreach (Match bloco in BlocoTransacaoRegex.Matches(conteudo))
            {
                var corpo = bloco.Groups[1].Value;

                var dataTexto = ExtrairTag(corpo, "DTPOSTED");
                var valorTexto = ExtrairTag(corpo, "TRNAMT");

                if (string.IsNullOrWhiteSpace(dataTexto) || string.IsNullOrWhiteSpace(valorTexto))
                    continue;

                // DTPOSTED vem como yyyyMMdd[hhmmss][.xxx][[-3:BRT]] — só a
                // data (8 primeiros dígitos) importa pra conciliação.
                var soData = new string(dataTexto.Take(8).ToArray());

                if (!DateTime.TryParseExact(
                        soData, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                    continue;

                if (!decimal.TryParse(
                        valorTexto, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var valor))
                    continue;

                var descricao = ExtrairTag(corpo, "MEMO") ?? ExtrairTag(corpo, "NAME") ?? "Lançamento importado";
                var fitId = ExtrairTag(corpo, "FITID");

                transacoes.Add(new OfxTransacao(data, valor, descricao, fitId));
            }

            return transacoes;
        }

        private static string? ExtrairTag(string corpo, string tag)
        {
            var match = Regex.Match(corpo, $"<{tag}>([^<\r\n]*)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}
