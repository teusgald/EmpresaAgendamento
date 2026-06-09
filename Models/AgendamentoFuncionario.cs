using EmpresaAgendamento.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmpresaAgendamento.Models
{
    public class AgendamentoFuncionario
    {
        public int AgendamentoId { get; set; }
        public Agendamento Agendamento { get; set; } = null!;

        public int FuncionarioId { get; set; }
        public Funcionario Funcionario { get; set; } = null!;

        public bool ResponsavelPrincipal { get; set; }

        public decimal? PercentualComissao { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ValorComissao { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ValorRecebido { get; set; }
    }
}