using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;

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
                    ServicosPopulares = servicosPopulares
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

                return View(empresa);
            }
            catch (Exception)
            {
                ToastHelper.Error(TempData, "Erro ao salvar empresa.");
                return RedirectToAction(nameof(Index));
            }
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