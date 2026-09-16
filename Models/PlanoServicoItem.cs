namespace EmpresaAgendamento.Models
{
    public class PlanoServicoItem
    {
        public int PlanoServicoId { get; set; }
        public PlanoServico PlanoServico { get; set; } = null!;

        public int ServicoId { get; set; }
        public Servico Servico { get; set; } = null!;
    }
}
