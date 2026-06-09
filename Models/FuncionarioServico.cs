using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Models
{
    public class FuncionarioServico
    {
        public int FuncionarioId { get; set; }

        public int ServicoId { get; set; }
        public Funcionario Funcionario { get; set; } = null!;

        public Servico Servico { get; set; } = null!;
    }
}