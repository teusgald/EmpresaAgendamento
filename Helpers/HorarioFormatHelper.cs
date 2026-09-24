using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Helpers
{
    // Junta dias consecutivos com o mesmo horário numa única linha
    // ("Seg a Sáb: 08:00–18:00 · Dom: Fechado"), do jeito que os
    // concorrentes mostram — em vez de listar dia por dia.
    public static class HorarioFormatHelper
    {
        private static readonly DayOfWeek[] OrdemSemana =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        private static readonly Dictionary<DayOfWeek, string> Abreviacao = new()
        {
            [DayOfWeek.Monday] = "Seg",
            [DayOfWeek.Tuesday] = "Ter",
            [DayOfWeek.Wednesday] = "Qua",
            [DayOfWeek.Thursday] = "Qui",
            [DayOfWeek.Friday] = "Sex",
            [DayOfWeek.Saturday] = "Sáb",
            [DayOfWeek.Sunday] = "Dom",
        };

        private static readonly Dictionary<DayOfWeek, string> NomeCompleto = new()
        {
            [DayOfWeek.Monday] = "Segunda",
            [DayOfWeek.Tuesday] = "Terça",
            [DayOfWeek.Wednesday] = "Quarta",
            [DayOfWeek.Thursday] = "Quinta",
            [DayOfWeek.Friday] = "Sexta",
            [DayOfWeek.Saturday] = "Sábado",
            [DayOfWeek.Sunday] = "Domingo",
        };

        // Uma linha por dia, sem agrupar ("Segunda: 08:00 às 18:00", "Terça: ...")
        // — é o formato pedido pra página pública, em vez de faixas ("Seg a Sex").
        public static List<string> FormatarPorDia(IEnumerable<EmpresaHorario> horarios)
        {
            var porDia = horarios.ToDictionary(h => h.DiaSemana);

            var linhas = new List<string>();

            foreach (var dia in OrdemSemana)
            {
                if (!porDia.TryGetValue(dia, out var horario))
                    continue;

                var rotuloHorario = !horario.TrabalhaNoDia
                    ? "Fechado"
                    : FormatarIntervalo(horario);

                linhas.Add($"{NomeCompleto[dia]}: {rotuloHorario}");
            }

            return linhas;
        }

        public static List<string> FormatarLinhas(IEnumerable<EmpresaHorario> horarios)
        {
            var porDia = horarios.ToDictionary(h => h.DiaSemana);

            var linhas = new List<string>();
            var diasOrdenados = OrdemSemana.Where(porDia.ContainsKey).ToList();

            for (var i = 0; i < diasOrdenados.Count;)
            {
                var diaInicio = diasOrdenados[i];
                var referencia = porDia[diaInicio];

                var j = i;
                while (j + 1 < diasOrdenados.Count && MesmoExpediente(referencia, porDia[diasOrdenados[j + 1]]))
                {
                    j++;
                }

                var diaFim = diasOrdenados[j];

                var rotuloDias = diaInicio == diaFim
                    ? Abreviacao[diaInicio]
                    : $"{Abreviacao[diaInicio]} a {Abreviacao[diaFim]}";

                var rotuloHorario = !referencia.TrabalhaNoDia
                    ? "Fechado"
                    : FormatarIntervalo(referencia);

                linhas.Add($"{rotuloDias}: {rotuloHorario}");

                i = j + 1;
            }

            return linhas;
        }

        private static bool MesmoExpediente(EmpresaHorario a, EmpresaHorario b)
        {
            if (a.TrabalhaNoDia != b.TrabalhaNoDia) return false;
            if (!a.TrabalhaNoDia) return true;

            return a.HoraInicio == b.HoraInicio
                && a.HoraFim == b.HoraFim
                && a.InicioIntervalo == b.InicioIntervalo
                && a.FimIntervalo == b.FimIntervalo;
        }

        private static string FormatarIntervalo(EmpresaHorario h)
        {
            var principal = $"{Fmt(h.HoraInicio)} às {Fmt(h.HoraFim)}";

            if (h.InicioIntervalo.HasValue && h.FimIntervalo.HasValue)
            {
                principal += $" (pausa {Fmt(h.InicioIntervalo.Value)} às {Fmt(h.FimIntervalo.Value)})";
            }

            return principal;
        }

        private static string Fmt(TimeSpan t) => t.ToString(@"hh\:mm");

        // Só o intervalo de hoje ("09:00 às 18:00" / "Fechado"), pro bloco de
        // info rápida da página pública — null quando o dia nem está
        // cadastrado (evita mostrar "Fechado" sem ter certeza).
        public static string? HorarioDeHoje(IEnumerable<EmpresaHorario> horarios, DateTime agora)
        {
            var horario = horarios.FirstOrDefault(h => h.DiaSemana == agora.DayOfWeek);

            if (horario == null)
                return null;

            return horario.TrabalhaNoDia ? FormatarIntervalo(horario) : "Fechado";
        }

        // "Aberto agora / fecha às.../ abre amanhã às..." pro selo da página
        // pública. `agora` entra por fora (em vez de DateTime.Now aqui dentro)
        // só pra dar pra testar com um horário fixo — a comparação em si é
        // sempre contra o relógio do servidor, sem fuso por empresa (esse
        // projeto não tem esse conceito, diferente de outros sistemas do
        // grupo). Texto vazio = sem horário cadastrado; a view decide não
        // mostrar o selo nesse caso, em vez de arriscar dizer "Fechado" errado.
        public static (bool Aberto, string Texto) ObterStatusAtual(IEnumerable<EmpresaHorario> horarios, DateTime agora)
        {
            var porDia = horarios.ToDictionary(h => h.DiaSemana);

            if (porDia.Count == 0)
                return (false, "");

            var hoje = agora.DayOfWeek;

            if (!porDia.TryGetValue(hoje, out var horarioHoje) || !horarioHoje.TrabalhaNoDia)
                return (false, DescreverProximaAbertura(porDia, hoje));

            var agoraHora = agora.TimeOfDay;

            if (horarioHoje.InicioIntervalo.HasValue && horarioHoje.FimIntervalo.HasValue &&
                agoraHora >= horarioHoje.InicioIntervalo.Value && agoraHora < horarioHoje.FimIntervalo.Value)
            {
                return (false, $"Em pausa · volta às {Fmt(horarioHoje.FimIntervalo.Value)}");
            }

            if (agoraHora >= horarioHoje.HoraInicio && agoraHora < horarioHoje.HoraFim)
                return (true, $"Aberto agora · fecha às {Fmt(horarioHoje.HoraFim)}");

            if (agoraHora < horarioHoje.HoraInicio)
                return (false, $"Fechado · abre hoje às {Fmt(horarioHoje.HoraInicio)}");

            return (false, DescreverProximaAbertura(porDia, hoje));
        }

        private static string DescreverProximaAbertura(Dictionary<DayOfWeek, EmpresaHorario> porDia, DayOfWeek hoje)
        {
            for (var i = 1; i <= 7; i++)
            {
                var dia = (DayOfWeek)(((int)hoje + i) % 7);

                if (!porDia.TryGetValue(dia, out var horario) || !horario.TrabalhaNoDia)
                    continue;

                var rotulo = i == 1 ? "amanhã" : NomeCompleto[dia].ToLowerInvariant();
                return $"Fechado · abre {rotulo} às {Fmt(horario.HoraInicio)}";
            }

            return "Fechado";
        }
    }
}
