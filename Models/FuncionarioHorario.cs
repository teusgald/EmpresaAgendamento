using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Models
{
    public class FuncionarioHorario
    {
        public int Id { get; set; }

        public int FuncionarioId { get; set; }
        public Funcionario Funcionario { get; set; } = null!;

        public DayOfWeek DiaSemana { get; set; }

        public TimeSpan HoraInicio { get; set; }

        public TimeSpan HoraFim { get; set; }

        public TimeSpan? InicioIntervalo { get; set; }

        public TimeSpan? FimIntervalo { get; set; }

        public bool TrabalhaNoDia { get; set; } = true;
    }
}