namespace EmpresaAgendamento.Models.ViewModels
{
    public class EmpresaSugestaoViewModel
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string? LogoUrl { get; set; }
        public string? Categoria { get; set; }
        public double? NotaMedia { get; set; }
        public int TotalAvaliacoes { get; set; }
        public string Slug { get; set; } = "";
        public int DescontosFidelidadeDisponiveis { get; set; }
    }

    public class ClienteInicioViewModel
    {
        public List<EmpresaSugestaoViewModel> Empresas { get; set; } = new();
        public List<string> Categorias { get; set; } = new();
        public string? CategoriaSelecionada { get; set; }
    }
}
