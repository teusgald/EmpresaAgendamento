namespace EmpresaAgendamento.Models.ViewModels
{
    public class FuncionarioHorarioItemViewModel
    {
        public DayOfWeek DiaSemana { get; set; }

        public bool TrabalhaNoDia { get; set; }

        public TimeSpan? HoraInicio { get; set; }

        public TimeSpan? HoraFim { get; set; }

        public TimeSpan? InicioIntervalo { get; set; }

        public TimeSpan? FimIntervalo { get; set; }
    }
}
