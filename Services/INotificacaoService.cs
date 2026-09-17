using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Services
{
    public interface INotificacaoService
    {
        // Aparece pro dono da empresa e pros funcionários.
        Task NotificarEmpresaAsync(int empresaId, TipoNotificacao tipo, string titulo, string mensagem, string? link = null);

        // Aparece só pro cliente dono do agendamento/pagamento em questão.
        Task NotificarClienteAsync(int clienteId, int empresaId, TipoNotificacao tipo, string titulo, string mensagem, string? link = null);
    }
}
