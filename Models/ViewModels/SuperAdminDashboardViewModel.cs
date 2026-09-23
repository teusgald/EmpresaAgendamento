namespace EmpresaAgendamento.Models.ViewModels
{
    public class SuperAdminDashboardViewModel
    {
        public int TotalEmpresas { get; set; }
        public int EmpresasPagantes { get; set; }
        public int EmpresasVip { get; set; }
        public int EmpresasNaoPagantes { get; set; }

        public int TotalFuncionarios { get; set; }
        public int TotalClientes { get; set; }

        public int TotalUsuarios { get; set; }
        public int UsuariosAtivos { get; set; }
    }
}
