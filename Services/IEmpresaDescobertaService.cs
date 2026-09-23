using EmpresaAgendamento.Models.ViewModels;

namespace EmpresaAgendamento.Services
{
    // Busca de empresas por categoria, cidade/UF, faixa de preço dos
    // serviços, avaliação mínima e distância — usada tanto pela tela de
    // sugestão do cliente logado (ClienteInicioController) quanto pela
    // página pública de descoberta (PublicoController.Empresas).
    public interface IEmpresaDescobertaService
    {
        Task<List<EmpresaSugestaoViewModel>> BuscarAsync(EmpresaDescobertaFiltro filtro);
    }
}
