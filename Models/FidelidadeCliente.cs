using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EmpresaAgendamento.Models
{
    // Progresso de um cliente específico num programa de fidelidade
    // específico — zera VisitasContadas (não DescontosDisponiveis) toda vez
    // que bate a meta e libera um desconto novo. Um cliente pode ter uma
    // linha por programa (empresa pode ter mais de um programa ativo).
    public class FidelidadeCliente
    {
        public int Id { get; set; }

        public int ProgramaFidelidadeId { get; set; }

        [ValidateNever]
        public ProgramaFidelidade Programa { get; set; } = null!;

        public int ClienteId { get; set; }

        [ValidateNever]
        public Cliente Cliente { get; set; } = null!;

        public int VisitasContadas { get; set; }

        public int DescontosDisponiveis { get; set; }

        public int DescontosUsados { get; set; }

        public DateTime? DataUltimaAtualizacao { get; set; }
    }
}
