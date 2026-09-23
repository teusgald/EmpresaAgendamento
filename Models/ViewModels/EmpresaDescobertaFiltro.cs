namespace EmpresaAgendamento.Models.ViewModels
{
    // Filtro compartilhado pela busca/descoberta de empresas — usado tanto na
    // tela de sugestão do cliente logado (ClienteInicioController) quanto na
    // página pública de descoberta (PublicoController.Empresas). Os nomes das
    // propriedades batem com os parâmetros de query string das duas telas, já
    // que os dois formulários (GET) fazem bind direto pra essa classe.
    public class EmpresaDescobertaFiltro
    {
        public string? Categoria { get; set; }

        public string? Cidade { get; set; }

        public string? UF { get; set; }

        public decimal? PrecoMinimo { get; set; }

        public decimal? PrecoMaximo { get; set; }

        public double? AvaliacaoMinima { get; set; }

        // Latitude/Longitude de quem está buscando (geolocalização do
        // navegador, opcional) — só entram no filtro de distância quando os
        // dois vierem preenchidos.
        public decimal? LatitudeUsuario { get; set; }

        public decimal? LongitudeUsuario { get; set; }

        // Só tem efeito quando LatitudeUsuario/LongitudeUsuario também vierem
        // preenchidos. Empresa sem Latitude/Longitude cadastrada nunca entra
        // no resultado quando esse filtro está ativo (não trava a busca
        // inteira, só fica de fora desse critério específico).
        public double? DistanciaMaximaKm { get; set; }

        // Quantidade máxima de empresas retornadas. 0 ou negativo = sem
        // limite (cada tela decide o próprio default/teto antes de chamar o
        // serviço).
        public int Limite { get; set; }
    }
}
