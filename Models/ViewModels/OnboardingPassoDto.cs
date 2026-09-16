namespace EmpresaAgendamento.Models.ViewModels
{
    public class OnboardingPassoDto
    {
        public string Titulo { get; set; } = null!;
        public string Descricao { get; set; } = null!;
        public bool Concluido { get; set; }
        public string Icone { get; set; } = null!;
        public string ControllerName { get; set; } = null!;
        public string ActionName { get; set; } = null!;
    }
}
