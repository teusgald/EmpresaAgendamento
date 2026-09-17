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
    }
}
