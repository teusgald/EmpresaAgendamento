using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Services
{
    public interface IAuditoriaService
    {
        // Nunca lança — uma falha ao gravar auditoria não pode travar a ação
        // de negócio que a originou (mesmo espírito defensivo do NotificacaoService).
        Task RegistrarAsync(
            int empresaId,
            ApplicationUser usuario,
            string entidade,
            int entidadeId,
            string acao,
            string? detalhe = null);
    }
}
