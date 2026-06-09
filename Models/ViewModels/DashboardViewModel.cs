using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalClientes { get; set; }

        public int TotalAgendamentosHoje { get; set; }

        public decimal FaturamentoMensal { get; set; }

        public decimal Crescimento { get; set; }

        public List<Agendamento> AgendamentosHoje { get; set; } = new();

        public List<FaturamentoMesDto> FaturamentoPorMes { get; set; } = new();

        public List<ServicoPopularDto> ServicosPopulares { get; set; } = new();
    }
}