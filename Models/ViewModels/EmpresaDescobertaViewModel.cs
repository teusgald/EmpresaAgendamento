namespace EmpresaAgendamento.Models.ViewModels
{
    // Tela pública de descoberta de empresas (PublicoController.Empresas,
    // rota /empresas) — mesma busca da sugestão do cliente logado
    // (IEmpresaDescobertaService), sem exigir login.
    public class EmpresaDescobertaViewModel
    {
        public List<EmpresaSugestaoViewModel> Empresas { get; set; } = new();
        public List<string> Categorias { get; set; } = new();
        public EmpresaDescobertaFiltro Filtro { get; set; } = new();
    }
}
