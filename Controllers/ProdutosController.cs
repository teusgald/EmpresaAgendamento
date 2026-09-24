using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    public class ProdutosController : Controller
    {
        // Mesmo padrão de validação/salvamento usado no logo da empresa
        // (ver EmpresasController.SalvarLogoAsync) — Content-Type primeiro
        // (o navegador detecta pelo conteúdo real), nome do arquivo só como fallback.
        private static readonly Dictionary<string, string> ExtensaoPorContentType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

        private static readonly string[] ExtensoesPermitidas = { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

        private const long TamanhoMaximoImagemBytes = 2 * 1024 * 1024; // 2 MB

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ProdutosController> _logger;

        public ProdutosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            ILogger<ProdutosController> logger)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _logger = logger;
        }

        private async Task<(bool Sucesso, string CaminhoOuErro)> SalvarFotoProdutoAsync(int produtoId, IFormFile fotoFile)
        {
            if (!ExtensaoPorContentType.TryGetValue(fotoFile.ContentType ?? "", out var extensao))
            {
                var extensaoArquivo = Path.GetExtension(fotoFile.FileName).ToLowerInvariant();

                if (extensaoArquivo == ".jpeg")
                    extensaoArquivo = ".jpg";

                if (!ExtensoesPermitidas.Contains(extensaoArquivo))
                {
                    return (false, "Formato de imagem inválido. Use PNG, JPG, WEBP ou GIF.");
                }

                extensao = extensaoArquivo;
            }

            if (fotoFile.Length > TamanhoMaximoImagemBytes)
            {
                return (false, "A imagem deve ter no máximo 2 MB.");
            }

            var pastaProduto = Path.Combine(_env.WebRootPath, "uploads", "produtos", produtoId.ToString());
            Directory.CreateDirectory(pastaProduto);

            foreach (var arquivoAntigo in Directory.GetFiles(pastaProduto, "foto.*"))
            {
                try { System.IO.File.Delete(arquivoAntigo); } catch { /* não bloqueia o upload por isso */ }
            }

            var nomeArquivo = $"foto{extensao}";
            var caminhoFisico = Path.Combine(pastaProduto, nomeArquivo);

            using (var stream = new FileStream(caminhoFisico, FileMode.Create))
            {
                await fotoFile.CopyToAsync(stream);
            }

            var caminhoPublico = $"/uploads/produtos/{produtoId}/{nomeArquivo}?v={DateTime.UtcNow.Ticks}";

            return (true, caminhoPublico);
        }

        private async Task<int?> GetEmpresaId()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                return user?.EmpresaId;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("/Produtos")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Produtos", "Visualizar" })]
        public async Task<IActionResult> Index(int page = 1)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada. Faça login novamente.");
                    return RedirectToAction("Login", "Account");
                }

                int pageSize = 10;

                var query = _context.Produtos
                    .Where(p => p.EmpresaId == empresaId)
                    .OrderBy(p => p.Nome);

                var totalItems = await query.CountAsync();

                var lista = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                return View(lista);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar produtos.");
                ToastHelper.Error(TempData, "Erro ao carregar produtos.");
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Produtos", "Criar" })]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Produtos", "Criar" })]
        public async Task<IActionResult> Create(Produto produto, IFormFile? fotoFile)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada. Faça login novamente.");
                    return RedirectToAction("Login", "Account");
                }

                ModelState.Remove("EmpresaId");
                ModelState.Remove("FotoUrl");

                if (!ModelState.IsValid)
                {
                    ToastHelper.Warning(TempData, "Preencha todos os campos corretamente.");
                    return View(produto);
                }

                produto.EmpresaId = empresaId.Value;
                produto.Ativo = true;
                produto.FotoUrl = null;

                _context.Produtos.Add(produto);
                await _context.SaveChangesAsync();

                if (fotoFile != null && fotoFile.Length > 0)
                {
                    var (sucesso, caminhoOuErro) = await SalvarFotoProdutoAsync(produto.Id, fotoFile);

                    if (sucesso)
                    {
                        produto.FotoUrl = caminhoOuErro;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        ToastHelper.Warning(TempData, $"Produto cadastrado, mas a foto não foi salva: {caminhoOuErro}");
                        return RedirectToAction(nameof(Index));
                    }
                }

                ToastHelper.Success(TempData, "Produto cadastrado com sucesso!");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar produto.");
                ToastHelper.Error(TempData, "Erro ao criar produto.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Produtos", "Editar" })]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var produto = await _context.Produtos
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (produto == null)
                {
                    ToastHelper.Error(TempData, "Produto não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                return View(produto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar produto {ProdutoId}.", id);
                ToastHelper.Error(TempData, "Erro ao carregar produto.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Produtos", "Editar" })]
        public async Task<IActionResult> Edit(int id, Produto produto, IFormFile? fotoFile)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                if (id != produto.Id)
                {
                    ToastHelper.Error(TempData, "Requisição inválida.");
                    return RedirectToAction(nameof(Index));
                }

                ModelState.Remove("EmpresaId");
                ModelState.Remove("FotoUrl");

                if (!ModelState.IsValid)
                {
                    ToastHelper.Warning(TempData, "Verifique os dados informados.");
                    return View(produto);
                }

                var existente = await _context.Produtos
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (existente == null)
                {
                    ToastHelper.Error(TempData, "Produto não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                existente.Nome = produto.Nome;
                existente.Descricao = produto.Descricao;
                existente.Preco = produto.Preco;
                existente.QuantidadeEstoque = produto.QuantidadeEstoque;

                string? erroFoto = null;

                if (fotoFile != null && fotoFile.Length > 0)
                {
                    var (sucesso, caminhoOuErro) = await SalvarFotoProdutoAsync(existente.Id, fotoFile);

                    if (sucesso)
                    {
                        existente.FotoUrl = caminhoOuErro;
                    }
                    else
                    {
                        erroFoto = caminhoOuErro;
                    }
                }

                // Salva o resto dos campos mesmo se a foto falhar — não faz
                // sentido descartar Nome/Preço/Estoque por causa da imagem.
                await _context.SaveChangesAsync();

                if (erroFoto != null)
                {
                    ToastHelper.Warning(TempData, $"Produto atualizado, mas a foto não foi salva: {erroFoto}");
                    return RedirectToAction(nameof(Index));
                }

                ToastHelper.Success(TempData, "Produto atualizado com sucesso!");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar produto {ProdutoId}.", id);
                ToastHelper.Error(TempData, "Erro ao atualizar produto.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Produtos", "Excluir" })]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var produto = await _context.Produtos
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (produto == null)
                {
                    ToastHelper.Error(TempData, "Produto não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                produto.Ativo = !produto.Ativo;

                await _context.SaveChangesAsync();

                ToastHelper.Success(
                    TempData,
                    produto.Ativo
                        ? "Produto ativado com sucesso!"
                        : "Produto inativado com sucesso!"
                );

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar status do produto {ProdutoId}.", id);
                ToastHelper.Error(TempData, "Erro ao alterar status.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
