namespace EmpresaAgendamento.Services
{
    public interface IFidelidadeService
    {
        Task RegistrarVisitaSeAplicavelAsync(int agendamentoId);
    }
}
