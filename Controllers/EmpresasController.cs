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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmpresasController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
        public async Task<IActionResult> Index(Empresa model)
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

                // MÍDIA
                empresa.LogoUrl = model.LogoUrl;
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

                ToastHelper.Success(TempData, "Dados salvos com sucesso!");

                return View(empresa);
            }
            catch (Exception)
            {
                ToastHelper.Error(TempData, "Erro ao salvar empresa.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}