using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Services
{
    public interface IClienteUnificacaoService
    {
        // Procura todo Cliente "órfão" (mesmo e-mail, sem conta própria —
        // UserId nulo) em qualquer empresa e unifica num só, preservando
        // agendamentos, fidelidade, avaliações, planos e contas a receber.
        // Retorna o Cliente sobrevivente (já salvo), ou null se não houver
        // nenhum órfão com esse e-mail — nesse caso é gente nova de verdade,
        // quem chamou deve criar um Cliente normalmente.
        Task<Cliente?> UnificarOrfaosAsync(string email);
    }
}
