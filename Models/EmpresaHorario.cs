namespace EmpresaAgendamento.Models
{
    public class EmpresaHorario
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        public DayOfWeek DiaSemana { get; set; }

        public TimeSpan HoraInicio { get; set; }

        public TimeSpan HoraFim { get; set; }

        public TimeSpan? InicioIntervalo { get; set; }

        public TimeSpan? FimIntervalo { get; set; }

        public bool TrabalhaNoDia { get; set; } = true;
    }
}
