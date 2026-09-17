using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    // Tela inicial do portal do cliente — sugestão de empresas (as mais bem
    // avaliadas, no geral ou por categoria) pra ele encontrar onde agendar.
    [Authorize(Roles = "Cliente")]
    [Route("Cliente")]
    public class ClienteInicioController : Controller
    {
        private const int LimiteSugestoes = 10;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClienteInicioController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? categoria)
        {
            // Slug só existe se a própria empresa preencheu manualmente em
            // Configurações — a maioria nunca fez isso, então ficava de fora
            // da lista (nem tinha como linkar pra página dela mesmo se
            // entrasse). Gera um automático pra quem não tem, uma vez só.
            await GarantirSlugsAsync();

            var categorias = await _context.Empresas
                .Where(e => e.Ativo && e.SegmentoAtuacao != null && e.SegmentoAtuacao != "")
                .Select(e => e.SegmentoAtuacao!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Empresa.NotaMedia/TotalAvaliacoes nunca são gravados em lugar
            // nenhum do sistema — a nota real vem sempre calculada na hora, a
            // partir da tabela de Avaliações (mesma conta feita na página
            // pública). Um LEFT JOIN aqui garante que a avaliação do próprio
            // cliente logado (ou de qualquer outro) entra na média, e que
            // empresa sem avaliação nenhuma ainda não desaparece da lista.
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

            if (!string.IsNullOrWhiteSpace(categoria))
            {
                query = query.Where(x => x.Empresa.SegmentoAtuacao == categoria);
            }

            // Mais bem avaliadas primeiro; sem avaliação nenhuma ainda, cai
            // pro cadastro mais recente — assim a lista nunca fica vazia
            // só porque ninguém avaliou ainda.
            var empresas = await query
                .OrderByDescending(x => x.NotaMedia ?? 0)
                .ThenByDescending(x => x.TotalAvaliacoes)
                .ThenByDescending(x => x.Empresa.DataCadastro)
                .Take(LimiteSugestoes)
                .Select(x => new EmpresaSugestaoViewModel
                {
                    Id = x.Empresa.Id,
                    Nome = x.Empresa.NomeFantasia ?? x.Empresa.Nome,
                    LogoUrl = x.Empresa.LogoUrl,
                    Categoria = x.Empresa.SegmentoAtuacao,
                    NotaMedia = x.NotaMedia,
                    TotalAvaliacoes = x.TotalAvaliacoes,
                    Slug = x.Empresa.Slug!
                })
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user?.ClienteId != null && empresas.Count > 0)
            {
                var idsDasEmpresas = empresas.Select(e => e.Id).ToList();

                // Um cliente pode ter desconto em mais de um programa da
                // mesma empresa (geral + de um serviço específico) — soma
                // tudo pra decidir se mostra o selo na empresa.
                var descontos = await _context.FidelidadeClientes
                    .Where(f => f.ClienteId == user.ClienteId && f.DescontosDisponiveis > 0
                        && idsDasEmpresas.Contains(f.Programa.EmpresaId))
                    .GroupBy(f => f.Programa.EmpresaId)
                    .Select(g => new { EmpresaId = g.Key, Total = g.Sum(f => f.DescontosDisponiveis) })
                    .ToDictionaryAsync(x => x.EmpresaId, x => x.Total);

                foreach (var empresa in empresas)
                {
                    if (descontos.TryGetValue(empresa.Id, out var disponiveis))
                    {
                        empresa.DescontosFidelidadeDisponiveis = disponiveis;
                    }
                }
            }

            var vm = new ClienteInicioViewModel
            {
                Empresas = empresas,
                Categorias = categorias,
                CategoriaSelecionada = categoria
            };

            return View(vm);
        }

        private async Task GarantirSlugsAsync()
        {
            var semSlug = await _context.Empresas
                .Where(e => e.Ativo && (e.Slug == null || e.Slug == ""))
                .ToListAsync();

            if (semSlug.Count == 0)
                return;

            var slugsExistentes = (await _context.Empresas
                    .Where(e => e.Slug != null && e.Slug != "")
                    .Select(e => e.Slug!.ToLower())
                    .ToListAsync())
                .ToHashSet();

            foreach (var empresa in semSlug)
            {
                var baseSlug = SlugHelper.Gerar(empresa.NomeFantasia ?? empresa.Nome);

                if (string.IsNullOrWhiteSpace(baseSlug))
                {
                    baseSlug = $"empresa-{empresa.Id}";
                }

                var candidato = baseSlug;
                var sufixo = 1;

                while (slugsExistentes.Contains(candidato))
                {
                    sufixo++;
                    candidato = $"{baseSlug}-{sufixo}";
                }

                empresa.Slug = candidato;
                slugsExistentes.Add(candidato);
            }

            await _context.SaveChangesAsync();
        }
    }
}
