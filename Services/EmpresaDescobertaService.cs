using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class EmpresaDescobertaService : IEmpresaDescobertaService
    {
        private readonly ApplicationDbContext _context;

        public EmpresaDescobertaService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<EmpresaSugestaoViewModel>> BuscarAsync(EmpresaDescobertaFiltro filtro)
        {
            filtro ??= new EmpresaDescobertaFiltro();

            // Empresa.NotaMedia/TotalAvaliacoes nunca são gravados em lugar
            // nenhum do sistema — a nota real vem sempre calculada na hora, a
            // partir da tabela de Avaliações. LEFT JOIN garante que empresa
            // sem avaliação nenhuma ainda não desaparece do resultado.
            var query =
                from e in _context.Empresas
                where e.Ativo && e.Slug != null && e.Slug != ""
                join a in _context.Avaliacoes on e.Id equals a.EmpresaId into avaliacoesDaEmpresa
                select new
                {
                    Empresa = e,
                    NotaMedia = avaliacoesDaEmpresa.Any()
                        ? (double?)avaliacoesDaEmpresa.Average(x => x.Nota)
                        : null,
                    TotalAvaliacoes = avaliacoesDaEmpresa.Count()
                };

            if (!string.IsNullOrWhiteSpace(filtro.Categoria))
            {
                query = query.Where(x => x.Empresa.SegmentoAtuacao == filtro.Categoria);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Cidade))
            {
                query = query.Where(x => x.Empresa.Cidade != null && x.Empresa.Cidade == filtro.Cidade);
            }

            if (!string.IsNullOrWhiteSpace(filtro.UF))
            {
                query = query.Where(x => x.Empresa.UF != null && x.Empresa.UF == filtro.UF);
            }

            if (filtro.PrecoMinimo.HasValue || filtro.PrecoMaximo.HasValue)
            {
                // Entra quem tiver PELO MENOS UM serviço ativo dentro da
                // faixa pedida — não exige que todo o cardápio da empresa
                // caiba no filtro.
                query = query.Where(x => x.Empresa.Servicos.Any(s =>
                    s.Ativo &&
                    (!filtro.PrecoMinimo.HasValue || s.Preco >= filtro.PrecoMinimo.Value) &&
                    (!filtro.PrecoMaximo.HasValue || s.Preco <= filtro.PrecoMaximo.Value)));
            }

            if (filtro.AvaliacaoMinima.HasValue)
            {
                query = query.Where(x => x.NotaMedia != null && x.NotaMedia >= filtro.AvaliacaoMinima.Value);
            }

            var resultado = await query
                .OrderByDescending(x => x.NotaMedia ?? 0)
                .ThenByDescending(x => x.TotalAvaliacoes)
                .ThenByDescending(x => x.Empresa.DataCadastro)
                .Select(x => new EmpresaSugestaoViewModel
                {
                    Id = x.Empresa.Id,
                    Nome = x.Empresa.NomeFantasia ?? x.Empresa.Nome,
                    LogoUrl = x.Empresa.LogoUrl,
                    CapaOuFotoUrl = x.Empresa.CapaBannerUrl ?? x.Empresa.Fotos
                        .OrderBy(f => f.DataUpload)
                        .Select(f => f.Url)
                        .FirstOrDefault(),
                    Categoria = x.Empresa.SegmentoAtuacao,
                    Cidade = x.Empresa.Cidade,
                    UF = x.Empresa.UF,
                    NotaMedia = x.NotaMedia,
                    TotalAvaliacoes = x.TotalAvaliacoes,
                    Slug = x.Empresa.Slug!,
                    Latitude = x.Empresa.Latitude,
                    Longitude = x.Empresa.Longitude
                })
                .ToListAsync();

            // Distância (Haversine) calculada em memória — depende de
            // latitude/longitude do usuário E da empresa. Empresa sem as duas
            // preenchidas simplesmente não entra nesse critério, sem travar o
            // resto da busca.
            if (filtro.LatitudeUsuario.HasValue && filtro.LongitudeUsuario.HasValue)
            {
                foreach (var empresa in resultado)
                {
                    if (empresa.Latitude.HasValue && empresa.Longitude.HasValue)
                    {
                        empresa.DistanciaKm = CalcularDistanciaKm(
                            (double)filtro.LatitudeUsuario.Value, (double)filtro.LongitudeUsuario.Value,
                            (double)empresa.Latitude.Value, (double)empresa.Longitude.Value);
                    }
                }

                if (filtro.DistanciaMaximaKm.HasValue)
                {
                    resultado = resultado
                        .Where(e => e.DistanciaKm.HasValue && e.DistanciaKm.Value <= filtro.DistanciaMaximaKm.Value)
                        .ToList();
                }

                resultado = resultado
                    .OrderBy(e => e.DistanciaKm ?? double.MaxValue)
                    .ThenByDescending(e => e.NotaMedia ?? 0)
                    .ToList();
            }

            if (filtro.Limite > 0 && resultado.Count > filtro.Limite)
            {
                resultado = resultado.Take(filtro.Limite).ToList();
            }

            return resultado;
        }

        // Fórmula de Haversine — distância em linha reta (km) entre dois
        // pontos geográficos, suficiente pra ordenar/filtrar "empresas perto
        // de mim" sem depender de serviço externo de rotas.
        private static double CalcularDistanciaKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double raioTerraKm = 6371;

            var dLat = ParaRadianos(lat2 - lat1);
            var dLon = ParaRadianos(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ParaRadianos(lat1)) * Math.Cos(ParaRadianos(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return raioTerraKm * c;
        }

        private static double ParaRadianos(double graus) => graus * Math.PI / 180;
    }
}
