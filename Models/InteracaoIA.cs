using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmpresaAgendamento.Models
{
    // Uma linha por mensagem (usuário ou modelo) do chat do Simpli AI.
    // Serve dois propósitos: reconstruir o histórico recente da conversa e
    // contar quota mensal contra Plano.LimiteIAMes (mesma convenção de
    // CountAsync + janela do mês usada pro limite de agendamentos).
    public class InteracaoIA
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        [ForeignKey(nameof(EmpresaId))]
        public Empresa Empresa { get; set; } = null!;

        // "user" ou "model" — mesmo vocabulário de role da API do Gemini.
        [Required]
        [StringLength(10)]
        public string Role { get; set; } = null!;

        [Required]
        public string Mensagem { get; set; } = null!;

        public DateTime CriadoEm { get; set; } = DateTime.Now;
    }
}
