using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using System.Text.RegularExpressions;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa")]
    public class EmpresasController : Controller
    {
        // Extensão de salvamento por Content-Type (fonte principal — é o que o
        // navegador detecta a partir do conteúdo real do arquivo, não do nome).
        private static readonly Dictionary<string, string> ExtensaoPorContentType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

        // Extensões aceitas no nome do arquivo — usado só como fallback quando
        // o Content-Type não vier reconhecível (alguns clientes não enviam certo).
        private static readonly string[] ExtensoesPermitidas = { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

        private const long TamanhoMaximoLogoBytes = 2 * 1024 * 1024; // 2 MB

        private const int LimiteFotosGaleria = 5;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public EmpresasController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // =========================
        // INDEX (GET)
        // =========================
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user?.EmpresaId == null)
                {
                    ToastHelper.Error(TempData, "Acesso negado.");
                    return RedirectToAction("Login", "Account");
                }

                var empresa = await _context.Empresas
                    .FirstOrDefaultAsync(e => e.Id == user.EmpresaId);

                if (empresa == null)
                {
                    ToastHelper.Error(TempData, "Empresa não encontrada.");
                    return NotFound();
                }

                ViewBag.Fotos = await _context.EmpresaFotos
                    .Where(f => f.EmpresaId == empresa.Id)
                    .OrderBy(f => f.DataUpload)
                    .ToListAsync();

                ViewBag.LimiteFotosGaleria = LimiteFotosGaleria;

                return View(empresa);
            }
            catch (Exception)
            {
                ToastHelper.Error(TempData, "Erro ao carregar empresa.");
                return RedirectToAction("Index", "Home");
            }
        }

        // =========================
        // DASHBOARD
        // =========================

        [Route("empresa/Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user?.EmpresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var empresaId = user.EmpresaId.Value;

                var assinaturaStatus = await _context.Empresas
                    .Where(e => e.Id == empresaId)
                    .Select(e => e.AssinaturaStatus)
                    .FirstOrDefaultAsync();

                var assinaturaAtiva = assinaturaStatus == "active" || assinaturaStatus == "trialing";

                var totalClientes = await _context.Clientes
                    .CountAsync(c =>
                        c.EmpresaClientes.Any(ec =>
                            ec.EmpresaId == empresaId));

                var agendamentosHoje = await _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.Funcionario)
                    .Where(a =>
                        a.EmpresaId == empresaId &&
                        a.Ativo &&
                        a.DataHora.Date == DateTime.Today)
                    .OrderBy(a => a.DataHora)
                    .ToListAsync();

                var faturamentoMensal = await _context.Agendamentos
                    .Include(a => a.Servico)
                    .Where(a =>
                        a.EmpresaId == empresaId &&
                        a.Status == StatusAgendamento.Finalizado &&
                        a.DataHora.Month == DateTime.Today.Month &&
                        a.DataHora.Year == DateTime.Today.Year)
                    .SumAsync(a => (decimal?)a.Servico.Preco) ?? 0;

                var dataAnterior = DateTime.Today.AddMonths(-1);

                var faturamentoMesAnterior = await _context.Agendamentos
                    .Include(a => a.Servico)
                    .Where(a =>
                        a.EmpresaId == empresaId &&
                        a.Status == StatusAgendamento.Finalizado &&
                        a.DataHora.Month == dataAnterior.Month &&
                        a.DataHora.Year == dataAnterior.Year)
                    .SumAsync(a => (decimal?)a.Servico.Preco) ?? 0;

                decimal crescimento = faturamentoMesAnterior == 0
                    ? 100
                    : ((faturamentoMensal - faturamentoMesAnterior)
                        / faturamentoMesAnterior) * 100;

                var faturamentoPorMes = await _context.Agendamentos
                    .Include(a => a.Servico)
                    .Where(a =>
                        a.EmpresaId == empresaId &&
                        a.Status == StatusAgendamento.Finalizado)
                    .GroupBy(a => a.DataHora.Month)
                    .Select(g => new FaturamentoMesDto
                    {
                        Mes = g.Key,
                        Total = g.Sum(a => a.Servico.Preco)
                    })
                    .ToListAsync();

                var servicosPopulares = await _context.Agendamentos
                    .Include(a => a.Servico)
                    .Where(a => a.EmpresaId == empresaId)
                    .GroupBy(a => a.Servico.Nome)
                    .Select(g => new ServicoPopularDto
                    {
                        Servico = g.Key,
                        Quantidade = g.Count()
                    })
                    .OrderByDescending(x => x.Quantidade)
                    .Take(6)
                    .ToListAsync();

                var vm = new DashboardViewModel
                {
                    TotalClientes = totalClientes,
                    TotalAgendamentosHoje = agendamentosHoje.Count,
                    FaturamentoMensal = faturamentoMensal,
                    Crescimento = crescimento,
                    AgendamentosHoje = agendamentosHoje,
                    FaturamentoPorMes = faturamentoPorMes,
                    ServicosPopulares = servicosPopulares,
                    OnboardingPassos = await CarregarPassosOnboardingAsync(empresaId),
                    AssinaturaAtiva = assinaturaAtiva
                };

                return View(vm);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar dashboard.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // ONBOARDING (checklist do dashboard)
        // =========================
        private async Task<List<OnboardingPassoDto>> CarregarPassosOnboardingAsync(int empresaId)
        {
            var empresa = await _context.Empresas
                .Where(e => e.Id == empresaId)
                .Select(e => new { e.LogoUrl })
                .FirstOrDefaultAsync();

            var temServico = await _context.Servicos
                .AnyAsync(s => s.EmpresaId == empresaId && s.Ativo);

            var temFuncionario = await _context.Funcionarios
                .AnyAsync(f => f.EmpresaId == empresaId && f.Ativo);

            var temHorarioConfigurado = await _context.FuncionariosHorarios
                .AnyAsync(h => h.Funcionario.EmpresaId == empresaId);

            return new List<OnboardingPassoDto>
            {
                new()
                {
                    Titulo = "Adicione a logo da empresa",
                    Descricao = "Deixe sua página pública com a cara do seu negócio.",
                    Concluido = !string.IsNullOrWhiteSpace(empresa?.LogoUrl),
                    Icone = "bi-image",
                    ControllerName = "Empresas",
                    ActionName = "Index"
                },
                new()
                {
                    Titulo = "Cadastre seus serviços",
                    Descricao = "O que sua empresa oferece — corte, manicure, consulta etc.",
                    Concluido = temServico,
                    Icone = "bi-scissors",
                    ControllerName = "Servicos",
                    ActionName = "Index"
                },
                new()
                {
                    Titulo = "Cadastre seus funcionários",
                    Descricao = "Quem vai atender os agendamentos.",
                    Concluido = temFuncionario,
                    Icone = "bi-person-badge",
                    ControllerName = "Funcionarios",
                    ActionName = "Index"
                },
                new()
                {
                    Titulo = "Configure o horário de trabalho",
                    Descricao = "Define os horários que aparecem disponíveis pros clientes.",
                    Concluido = temHorarioConfigurado,
                    Icone = "bi-clock",
                    ControllerName = "Funcionarios",
                    ActionName = "Index"
                }
            };
        }

        // =========================
        // INDEX POST (EDIT EMPRESA)
        // =========================
        [HttpPost("index")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Empresa model, IFormFile? logoFile)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user?.EmpresaId == null)
                {
                    ToastHelper.Error(TempData, "Acesso negado.");
                    return RedirectToAction("Login", "Account");
                }

                var empresa = await _context.Empresas
                    .FirstOrDefaultAsync(e => e.Id == user.EmpresaId);

                if (empresa == null)
                {
                    ToastHelper.Error(TempData, "Empresa não encontrada.");
                    return NotFound();
                }

                // IDENTIDADE
                empresa.Nome = model.Nome;
                empresa.NomeFantasia = model.NomeFantasia;
                empresa.Slogan = model.Slogan;
                empresa.Descricao = model.Descricao;
                empresa.SegmentoAtuacao = model.SegmentoAtuacao;
                empresa.AnoFundacao = model.AnoFundacao;

                empresa.DocumentoNumero = model.DocumentoNumero;
                empresa.DocumentoTipo = model.DocumentoTipo;

                // MÍDIA — logo é enviada como arquivo (não mais URL digitada).
                // Sem arquivo novo, mantém a logo que a empresa já tinha.
                string? avisoLogo = null;

                if (logoFile != null && logoFile.Length > 0)
                {
                    var (sucesso, caminhoOuErro) = await SalvarLogoAsync(empresa.Id, logoFile);

                    if (sucesso)
                    {
                        empresa.LogoUrl = caminhoOuErro;
                    }
                    else
                    {
                        avisoLogo = caminhoOuErro;
                    }
                }

                empresa.CapaBannerUrl = model.CapaBannerUrl;

                // ENDEREÇO
                empresa.Endereco = model.Endereco;
                empresa.Numero = model.Numero;
                empresa.Complemento = model.Complemento;
                empresa.Bairro = model.Bairro;
                empresa.Cidade = model.Cidade;
                empresa.UF = model.UF;
                empresa.CEP = model.CEP;
                empresa.Latitude = model.Latitude;
                empresa.Longitude = model.Longitude;
                empresa.LinkMaps = model.LinkMaps;

                // CONTATO
                empresa.Telefone = model.Telefone;
                empresa.WhatsApp = model.WhatsApp;
                empresa.Email = model.Email;
                empresa.EmailContato = model.EmailContato;

                // DIGITAL
                empresa.Instagram = model.Instagram;
                empresa.Facebook = model.Facebook;
                empresa.TikTok = model.TikTok;
                empresa.YouTube = model.YouTube;
                empresa.Site = model.Site;
                empresa.Slug = model.Slug;
                empresa.LinkAgendamentoExterno = model.LinkAgendamentoExterno;

                // PAGAMENTOS
                empresa.AceitaPix = model.AceitaPix;
                empresa.AceitaDinheiro = model.AceitaDinheiro;
                empresa.AceitaCartaoDebito = model.AceitaCartaoDebito;
                empresa.AceitaCartaoCredito = model.AceitaCartaoCredito;
                empresa.ParcelamentoMaximo = model.ParcelamentoMaximo;

                // COMODIDADES
                empresa.TemWifi = model.TemWifi;
                empresa.TemEstacionamento = model.TemEstacionamento;
                empresa.TemAcessibilidade = model.TemAcessibilidade;
                empresa.TemArCondicionado = model.TemArCondicionado;
                empresa.AtendimentoOnline = model.AtendimentoOnline;
                empresa.ComodidadesExtras = model.ComodidadesExtras;

                empresa.Ativo = model.Ativo;

                await _context.SaveChangesAsync();

                if (avisoLogo != null)
                {
                    ToastHelper.Warning(TempData, $"Dados salvos, mas a logo não foi atualizada: {avisoLogo}");
                }
                else
                {
                    ToastHelper.Success(TempData, "Dados salvos com sucesso!");
                }

                // Redireciona (não retorna a View direto) porque o GET Index
                // é quem monta ViewBag.Fotos/LimiteFotosGaleria pra galeria —
                // sem isso a página quebrava ao salvar (ViewBag.Fotos nulo).
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ToastHelper.Error(TempData, "Erro ao salvar empresa.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // GALERIA — UPLOAD
        // =========================
        [HttpPost("galeria/upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadGaleria(List<IFormFile> fotos)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user?.EmpresaId == null)
            {
                ToastHelper.Error(TempData, "Acesso negado.");
                return RedirectToAction("Login", "Account");
            }

            var empresa = await _context.Empresas
                .FirstOrDefaultAsync(e => e.Id == user.EmpresaId);

            if (empresa == null)
            {
                ToastHelper.Error(TempData, "Empresa não encontrada.");
                return NotFound();
            }

            if (fotos == null || fotos.Count == 0 || fotos.All(f => f.Length == 0))
            {
                ToastHelper.Warning(TempData, "Selecione ao menos uma foto.");
                return RedirectToAction(nameof(Index));
            }

            var totalAtual = await _context.EmpresaFotos.CountAsync(f => f.EmpresaId == empresa.Id);

            if (totalAtual + fotos.Count > LimiteFotosGaleria)
            {
                ToastHelper.Warning(
                    TempData,
                    $"A galeria aceita no máximo {LimiteFotosGaleria} fotos — você já tem {totalAtual}.");

                return RedirectToAction(nameof(Index));
            }

            var nomePasta = NomePastaGaleria(empresa);
            var erros = new List<string>();

            foreach (var foto in fotos)
            {
                if (foto.Length == 0) continue;

                var (sucesso, caminhoOuErro) = await SalvarFotoGaleriaAsync(nomePasta, foto);

                if (sucesso)
                {
                    _context.EmpresaFotos.Add(new EmpresaFoto
                    {
                        EmpresaId = empresa.Id,
                        Url = caminhoOuErro
                    });
                }
                else
                {
                    erros.Add(caminhoOuErro);
                }
            }

            await _context.SaveChangesAsync();

            if (erros.Any())
            {
                ToastHelper.Warning(TempData, $"Algumas fotos não foram salvas: {string.Join(" ", erros)}");
            }
            else
            {
                ToastHelper.Success(TempData, "Fotos adicionadas à galeria!");
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // GALERIA — REMOVER
        // =========================
        [HttpPost("galeria/remover")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverFotoGaleria(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user?.EmpresaId == null)
            {
                ToastHelper.Error(TempData, "Acesso negado.");
                return RedirectToAction("Login", "Account");
            }

            var foto = await _context.EmpresaFotos
                .FirstOrDefaultAsync(f => f.Id == id && f.EmpresaId == user.EmpresaId);

            if (foto == null)
            {
                ToastHelper.Error(TempData, "Foto não encontrada.");
                return RedirectToAction(nameof(Index));
            }

            var caminhoFisico = Path.Combine(
                _env.WebRootPath,
                foto.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            try
            {
                if (System.IO.File.Exists(caminhoFisico))
                {
                    System.IO.File.Delete(caminhoFisico);
                }
            }
            catch
            {
                // Não bloqueia a remoção do registro por falha ao apagar o arquivo.
            }

            _context.EmpresaFotos.Remove(foto);
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Foto removida.");
            return RedirectToAction(nameof(Index));
        }

        // Pasta da galeria é nomeada pelo "nome" da empresa (o slug). O slug é
        // texto livre digitado pelo dono no perfil — nunca dá pra usar direto
        // num caminho de arquivo (ex.: "../../windows" seria path traversal).
        // Só letra/número/hífen/underscore sobrevivem; o resto vira "-".
        private static string NomePastaGaleria(Empresa empresa)
        {
            var baseNome = string.IsNullOrWhiteSpace(empresa.Slug)
                ? $"empresa-{empresa.Id}"
                : empresa.Slug;

            var sanitizado = Regex.Replace(baseNome, @"[^a-zA-Z0-9\-_]", "-").Trim('-');

            return string.IsNullOrWhiteSpace(sanitizado) ? $"empresa-{empresa.Id}" : sanitizado;
        }

        // Salva em wwwroot/uploads/galeria/{nomePasta}/{guid}{ext} — cria a
        // pasta se não existir. Cada foto tem nome único (não substitui as
        // outras, diferente da logo).
        private async Task<(bool Sucesso, string CaminhoOuErro)> SalvarFotoGaleriaAsync(string nomePasta, IFormFile foto)
        {
            if (!ExtensaoPorContentType.TryGetValue(foto.ContentType ?? "", out var extensao))
            {
                var extensaoArquivo = Path.GetExtension(foto.FileName).ToLowerInvariant();

                if (extensaoArquivo == ".jpeg")
                    extensaoArquivo = ".jpg";

                if (!ExtensoesPermitidas.Contains(extensaoArquivo))
                {
                    return (false, "Formato de imagem inválido. Use PNG, JPG, WEBP ou GIF.");
                }

                extensao = extensaoArquivo;
            }

            if (foto.Length > TamanhoMaximoLogoBytes)
            {
                return (false, "Cada imagem deve ter no máximo 2 MB.");
            }

            var pastaGaleria = Path.Combine(_env.WebRootPath, "uploads", "galeria", nomePasta);
            Directory.CreateDirectory(pastaGaleria);

            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoFisico = Path.Combine(pastaGaleria, nomeArquivo);

            using (var stream = new FileStream(caminhoFisico, FileMode.Create))
            {
                await foto.CopyToAsync(stream);
            }

            var caminhoPublico = $"/uploads/galeria/{nomePasta}/{nomeArquivo}";

            return (true, caminhoPublico);
        }

        // Salva a logo enviada em wwwroot/uploads/empresas/{empresaId}/logo{ext},
        // sempre com esse mesmo nome (reenviar substitui a anterior — não fica
        // lixo acumulando). Nome/extensão nunca vêm do arquivo do usuário.
        private async Task<(bool Sucesso, string CaminhoOuErro)> SalvarLogoAsync(int empresaId, IFormFile logoFile)
        {
            // Content-Type primeiro (o navegador detecta pelo conteúdo real do
            // arquivo); nome do arquivo só como reforço/fallback.
            if (!ExtensaoPorContentType.TryGetValue(logoFile.ContentType ?? "", out var extensao))
            {
                var extensaoArquivo = Path.GetExtension(logoFile.FileName).ToLowerInvariant();

                if (extensaoArquivo == ".jpeg")
                    extensaoArquivo = ".jpg";

                if (!ExtensoesPermitidas.Contains(extensaoArquivo))
                {
                    return (false, "Formato de imagem inválido. Use PNG, JPG, WEBP ou GIF.");
                }

                extensao = extensaoArquivo;
            }

            if (logoFile.Length > TamanhoMaximoLogoBytes)
            {
                return (false, "A imagem deve ter no máximo 2 MB.");
            }

            var pastaEmpresa = Path.Combine(_env.WebRootPath, "uploads", "empresas", empresaId.ToString());
            Directory.CreateDirectory(pastaEmpresa);

            // Remove qualquer logo antiga (pode ter extensão diferente da nova).
            foreach (var arquivoAntigo in Directory.GetFiles(pastaEmpresa, "logo.*"))
            {
                try { System.IO.File.Delete(arquivoAntigo); } catch { /* não bloqueia o upload por isso */ }
            }

            var nomeArquivo = $"logo{extensao}";
            var caminhoFisico = Path.Combine(pastaEmpresa, nomeArquivo);

            using (var stream = new FileStream(caminhoFisico, FileMode.Create))
            {
                await logoFile.CopyToAsync(stream);
            }

            var caminhoPublico = $"/uploads/empresas/{empresaId}/{nomeArquivo}?v={DateTime.UtcNow.Ticks}";

            return (true, caminhoPublico);
        }
    }
}