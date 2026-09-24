using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
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
        private readonly IEmpresaDescobertaService _empresaDescobertaService;
        private readonly ILogger<ClienteInicioController> _logger;

        public ClienteInicioController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmpresaDescobertaService empresaDescobertaService,
            ILogger<ClienteInicioController> logger)
        {
            _context = context;
            _userManager = userManager;
            _empresaDescobertaService = empresaDescobertaService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] EmpresaDescobertaFiltro filtro)
        {
            try
            {
            filtro ??= new EmpresaDescobertaFiltro();
            filtro.Limite = LimiteSugestoes;

            // Slug só existe se a própria empresa preencheu manualmente em
            // Configurações — a maioria nunca fez isso, então ficava de fora
            // da lista (nem tinha como linkar pra página dela mesmo se
            // entrasse). Gera um automático pra quem não tem, uma vez só.
            await GarantirSlugsAsync();

            var categorias = CategoriaEmpresaHelper.Opcoes;

            var empresas = await _empresaDescobertaService.BuscarAsync(filtro);

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
                Categorias = categorias.ToList(),
                CategoriaSelecionada = filtro.Categoria,
                NomeBuscado = filtro.Nome,
                Cidade = filtro.Cidade,
                UF = filtro.UF,
                PrecoMinimo = filtro.PrecoMinimo,
                PrecoMaximo = filtro.PrecoMaximo,
                AvaliacaoMinima = filtro.AvaliacaoMinima,
                DistanciaMaximaKm = filtro.DistanciaMaximaKm
            };

            return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao carregar sugestões de empresas para o cliente.");
                ToastHelper.Error(TempData, "Erro ao carregar sugestões de empresas.");
                return View(new ClienteInicioViewModel());
            }
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

                // Slug reservado (ex.: "empresas", "login") nunca pode virar
                // slug de empresa — a rota literal sempre vence e a página
                // dela ficaria inacessível em /{slug}.
                while (slugsExistentes.Contains(candidato) || SlugHelper.EhReservado(candidato))
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
