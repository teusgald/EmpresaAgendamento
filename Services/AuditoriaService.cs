using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Services
{
    public class AuditoriaService : IAuditoriaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditoriaService> _logger;

        public AuditoriaService(ApplicationDbContext context, ILogger<AuditoriaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RegistrarAsync(
            int empresaId,
            ApplicationUser usuario,
            string entidade,
            int entidadeId,
            string acao,
            string? detalhe = null)
        {
            try
            {
                _context.AuditoriasAcesso.Add(new AuditoriaAcesso
                {
                    EmpresaId = empresaId,
                    UsuarioId = usuario.Id,
                    UsuarioNome = usuario.NomeCompleto ?? usuario.Email,
                    Entidade = entidade,
                    EntidadeId = entidadeId,
                    Acao = acao,
                    Detalhe = detalhe
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Auditoria é um efeito colateral — nunca pode derrubar a
                // ação de negócio (editar/desativar cliente) que a originou.
                _logger.LogError(
                    ex,
                    "Falha ao registrar auditoria (empresa {EmpresaId}, {Entidade} {EntidadeId}, ação {Acao}).",
                    empresaId, entidade, entidadeId, acao);
            }
        }
    }
}
