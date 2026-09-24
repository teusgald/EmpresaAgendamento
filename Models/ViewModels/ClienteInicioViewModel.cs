using EmpresaAgendamento.Models.Enums;

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

        // Adicionados para a busca/descoberta de empresas (filtro de
        // cidade/UF, distância e card com foto de destaque) — nunca
        // preenchidos por quem montava esse ViewModel antes disso existir,
        // então ficam null/default sem quebrar nenhum uso atual.
        public string? CapaOuFotoUrl { get; set; }
        public string? Cidade { get; set; }
        public string? UF { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Só preenchido quando o filtro de localização (latitude/longitude
        // do usuário) foi usado na busca.
        public double? DistanciaKm { get; set; }
    }

    public class ClienteInicioViewModel
    {
        public List<EmpresaSugestaoViewModel> Empresas { get; set; } = new();
        public List<(CategoriaEmpresa Valor, string Nome)> Categorias { get; set; } = new();
        public CategoriaEmpresa? CategoriaSelecionada { get; set; }

        // Espelham o EmpresaDescobertaFiltro usado na busca — só pra
        // repopular o formulário de filtro na tela com o que o cliente
        // escolheu.
        public string? NomeBuscado { get; set; }
        public string? Cidade { get; set; }
        public string? UF { get; set; }
        public decimal? PrecoMinimo { get; set; }
        public decimal? PrecoMaximo { get; set; }
        public double? AvaliacaoMinima { get; set; }
        public double? DistanciaMaximaKm { get; set; }
    }
}
