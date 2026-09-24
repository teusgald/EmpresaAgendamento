using System.Text.RegularExpressions;

namespace EmpresaAgendamento.Helpers
{
    // Calcula as variáveis CSS de accent (--accent, --accent2, --accent-contrast)
    // a partir das duas cores de marca que a empresa escolhe
    // (Empresa.CorPrimaria/CorSecundaria). Centralizado aqui — antes vivia
    // inline em Views/Publico/index.cshtml — pra ter um único lugar quando
    // outra tela (ex.: Empresas.cshtml, /cliente/login) precisar da mesma
    // marca. Fundo/texto/borda da página NÃO entram aqui de propósito — ver
    // o comentário de GerarVariaveisTema.
    public static class CorMarcaHelper
    {
        public static bool EhHexValido(string? hex) =>
            !string.IsNullOrWhiteSpace(hex) && Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$");

        private static (int R, int G, int B) HexParaRgb(string hex) => (
            Convert.ToInt32(hex.Substring(1, 2), 16),
            Convert.ToInt32(hex.Substring(3, 2), 16),
            Convert.ToInt32(hex.Substring(5, 2), 16));

        // Mistura linear simples (mesmo resultado do color-mix() do CSS em
        // srgb) — só pra gerar o tom de apoio quando não tem secundária
        // definida (nunca pra texto/fundo direto, ver MelhorContraste).
        private static string Misturar(string hexA, string hexB, double pctA)
        {
            var (ar, ag, ab) = HexParaRgb(hexA);
            var (br, bg, bb) = HexParaRgb(hexB);
            var p = Math.Clamp(pctA, 0, 100) / 100.0;

            int r = (int)Math.Round(ar * p + br * (1 - p));
            int g = (int)Math.Round(ag * p + bg * (1 - p));
            int b = (int)Math.Round(ab * p + bb * (1 - p));

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        // Luminância relativa (WCAG 2.x) — base do cálculo de contraste real
        // abaixo, em vez do corte fixo "luminância do fundo > 0.6" que a
        // página pública usava antes: aquele corte decide só pela cor de
        // fundo, sem checar se o texto resultante realmente lê bem em cima
        // dela — erra justamente nas cores de saturação média, no meio da
        // faixa entre "claro" e "escuro".
        private static double LuminanciaRelativa(string hex)
        {
            var (r, g, b) = HexParaRgb(hex);

            double Canal(int c)
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Canal(r) + 0.7152 * Canal(g) + 0.0722 * Canal(b);
        }

        private static double RazaoContraste(string hexA, string hexB)
        {
            var la = LuminanciaRelativa(hexA);
            var lb = LuminanciaRelativa(hexB);
            var (claro, escuro) = la >= lb ? (la, lb) : (lb, la);
            return (claro + 0.05) / (escuro + 0.05);
        }

        // Escolhe preto ou branco puro pra texto em cima de `fundo` testando
        // o contraste real dos dois, em vez de decidir só pela luminância do
        // fundo — cobre o caso de cores no meio da faixa onde os dois "quase
        // empatam".
        private static string MelhorContraste(string fundo) =>
            RazaoContraste(fundo, "#111111") >= RazaoContraste(fundo, "#ffffff")
                ? "#111111"
                : "#ffffff";

        // CSS aplicado em :root/body.light-theme na página pública.
        //
        // Antes essa função também sobrescrevia --bg/--text/--border com a cor
        // primária (fundo da página = a cor em si). Na prática isso deixava o
        // layout ilegível/feio pra maioria das cores reais de cliente: cor de
        // marca quase nunca foi escolhida pra funcionar como fundo de página
        // inteira (pensa nas cores de Coca-Cola, Spotify, Nubank — nenhuma
        // usa a própria cor como fundo do app; usam preto/branco/cinza com a
        // cor como DESTAQUE). Corrigido: fundo/texto/borda ficam SEMPRE no
        // tema padrão do Simpli Time (claro/escuro, já testado, sempre
        // legível) — a marca do cliente entra só como accent:
        //
        //   --accent  (primária): cor de ação — botão principal (Agendar
        //     agora), aba ativa, campo em foco, ícone informativo, borda do
        //     avatar, selo de nota, link.
        //   --accent2 (secundária, opcional): cor de apoio — hover dos
        //     elementos acima, categoria/segmento no cabeçalho, o outro tom
        //     do anel do logo. Sem secundária definida, cai num tom mais
        //     claro/escuro derivado da própria primária (era assim antes,
        //     mantido).
        //
        // Com isso as duas cores aparecem de um jeito que "tem a cara do
        // cliente" sem arriscar o resto do layout — pontos de destaque
        // pontuais, nunca a superfície inteira.
        public static string GerarVariaveisTema(string? corPrimaria, string? corSecundaria)
        {
            if (!EhHexValido(corPrimaria))
                return "";

            var primaria = corPrimaria!;
            var textoBase = MelhorContraste(primaria);

            var temSecundaria = EhHexValido(corSecundaria);
            var detalhe2 = temSecundaria ? corSecundaria! : Misturar(primaria, textoBase, 75);
            var detalheContraste = MelhorContraste(primaria);

            return $"--accent:{primaria};--accent2:{detalhe2};--accent-contrast:{detalheContraste};";
        }

        // Mesma ideia, só pro botão do formulário de login/cadastro que abre
        // dentro do modal da página pública — o card do modal fica sempre
        // branco (tela reaproveitada de /cliente/login, que tem seu próprio
        // contraste garantido), mas o botão de ação segue a mesma primária
        // usada em --accent no resto da página (era a secundária antes —
        // ficava um botão de cor diferente do "Agendar agora" da própria
        // página que abriu o modal).
        public static string GerarVariaveisLogin(string? corPrimaria, string? corSecundaria)
        {
            if (!EhHexValido(corPrimaria))
                return "";

            var primaria = corPrimaria!;
            var textoBase = MelhorContraste(primaria);
            var temSecundaria = EhHexValido(corSecundaria);
            var hover = Misturar(primaria, textoBase, 85);
            var contraste = MelhorContraste(primaria);
            var acentoSuave = temSecundaria ? corSecundaria! : primaria;

            return
                $"--login-btn:{primaria};--login-btn-hover:{hover};" +
                $"--login-btn-contrast:{contraste};--login-accent-soft:{acentoSuave}22;";
        }
    }
}
