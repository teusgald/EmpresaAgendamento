using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.Extensions.Logging;

namespace EmpresaAgendamento.Services
{
    public class NotificacaoService : INotificacaoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificacaoService> _logger;

        public NotificacaoService(ApplicationDbContext context, ILogger<NotificacaoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Task NotificarEmpresaAsync(int empresaId, TipoNotificacao tipo, string titulo, string mensagem, string? link = null)
            => CriarAsync(empresaId, null, tipo, titulo, mensagem, link);

        public Task NotificarClienteAsync(int clienteId, int empresaId, TipoNotificacao tipo, string titulo, string mensagem, string? link = null)
            => CriarAsync(empresaId, clienteId, tipo, titulo, mensagem, link);

        private async Task CriarAsync(int empresaId, int? clienteId, TipoNotificacao tipo, string titulo, string mensagem, string? link)
        {
            try
            {
                _context.Notificacoes.Add(new Notificacao
                {
                    EmpresaId = empresaId,
                    ClienteId = clienteId,
                    Tipo = tipo,
                    Titulo = titulo,
                    Mensagem = mensagem,
                    Link = link
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Notificação é um efeito colateral — nunca pode derrubar a
                // ação principal (agendamento, pagamento etc.) que a disparou.
                _logger.LogError(ex, "Falha ao criar notificação (empresa {EmpresaId}, cliente {ClienteId}, tipo {Tipo}).", empresaId, clienteId, tipo);
            }
        }
    }
}
