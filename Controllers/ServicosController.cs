using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    public class ServicosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ServicosController> _logger;

        public ServicosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ServicosController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
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

        // Quando o Perfil do funcionário tem Escopo "Próprios" em Serviços,
        // devolve os Ids dos serviços que ele está vinculado a realizar
        // (FuncionarioServico) — a lista some no menos de Serviços que ele
        // não faz. Retorna null quando não deve restringir (dono da empresa,
        // Escopo "Todos", ou funcionário sem perfil — esse último já nem
        // chega aqui, o RequerPermissaoFilter barra antes).
        private async Task<List<int>?> GetServicoIdsRestritosAsync()
        {
            if (!User.IsInRole("Funcionario"))
                return null;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            var funcionario = await _context.Funcionarios
                .Where(f => f.UserId == user.Id)
                .Select(f => new { f.Id, f.PerfilId })
                .FirstOrDefaultAsync();

            if (funcionario?.PerfilId == null)
                return null;

            var escopo = await _context.PerfilPermissoes
                .Where(p => p.PerfilId == funcionario.PerfilId && p.Modulo == "Servicos")
                .Select(p => (EscopoDadosPerfil?)p.EscopoDados)
                .FirstOrDefaultAsync();

            if (escopo != EscopoDadosPerfil.Proprios)
                return null;

            return await _context.FuncionariosServicos
                .Where(fs => fs.FuncionarioId == funcionario.Id)
                .Select(fs => fs.ServicoId)
                .ToListAsync();
        }

        // =========================
        // INDEX
        // =========================
        [HttpGet("/Servicos")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Servicos", "Visualizar" })]
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

                var servicoIdsRestritos = await GetServicoIdsRestritosAsync();

                var query = _context.Servicos
                    .Where(s => s.EmpresaId == empresaId)
                    .Where(s => servicoIdsRestritos == null || servicoIdsRestritos.Contains(s.Id))
                    .OrderBy(s => s.Nome);

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
                _logger.LogError(ex, "Erro ao carregar serviços.");
                ToastHelper.Error(TempData, "Erro ao carregar serviços.");
                return RedirectToAction("Index", "Home");
            }
        }

        // =========================
        // CREATE GET
        // =========================
        [HttpGet]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Servicos", "Criar" })]
        public IActionResult Create()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao abrir formulário de criação de serviço.");
                ToastHelper.Error(TempData, "Erro ao abrir formulário.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Servicos", "Criar" })]
        public async Task<IActionResult> Create(Servico servico)
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

                if (!ModelState.IsValid)
                {
                    ToastHelper.Warning(TempData, "Preencha todos os campos corretamente.");
                    return View(servico);
                }

                servico.EmpresaId = empresaId.Value;
                servico.Ativo = true;

                _context.Servicos.Add(servico);
                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Serviço cadastrado com sucesso!");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar serviço.");
                ToastHelper.Error(TempData, "Erro ao criar serviço.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // EDIT GET
        // =========================
        [HttpGet]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Servicos", "Editar" })]
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

                var servico = await _context.Servicos
                    .FirstOrDefaultAsync(s =>
                        s.Id == id &&
                        s.EmpresaId == empresaId);

                if (servico == null)
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                var servicoIdsRestritos = await GetServicoIdsRestritosAsync();

                if (servicoIdsRestritos != null && !servicoIdsRestritos.Contains(servico.Id))
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                return View(servico);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar serviço {ServicoId}.", id);
                ToastHelper.Error(TempData, "Erro ao carregar serviço.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // EDIT POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Servicos", "Editar" })]
        public async Task<IActionResult> Edit(int id, Servico servico)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                if (id != servico.Id)
                {
                    ToastHelper.Error(TempData, "Requisição inválida.");
                    return RedirectToAction(nameof(Index));
                }

                ModelState.Remove("EmpresaId");

                if (!ModelState.IsValid)
                {
                    ToastHelper.Warning(TempData, "Verifique os dados informados.");
                    return View(servico);
                }

                var existente = await _context.Servicos
                    .FirstOrDefaultAsync(s =>
                        s.Id == id &&
                        s.EmpresaId == empresaId);

                if (existente == null)
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                var servicoIdsRestritos = await GetServicoIdsRestritosAsync();

                if (servicoIdsRestritos != null && !servicoIdsRestritos.Contains(existente.Id))
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                existente.Nome = servico.Nome;
                existente.Preco = servico.Preco;
                existente.DuracaoMinutos = servico.DuracaoMinutos;

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Serviço atualizado com sucesso!");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar serviço {ServicoId}.", id);
                ToastHelper.Error(TempData, "Erro ao atualizar serviço.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // TOGGLE ATIVO
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Servicos", "Excluir" })]
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

                var servico = await _context.Servicos
                    .FirstOrDefaultAsync(s =>
                        s.Id == id &&
                        s.EmpresaId == empresaId);

                if (servico == null)
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                var servicoIdsRestritos = await GetServicoIdsRestritosAsync();

                if (servicoIdsRestritos != null && !servicoIdsRestritos.Contains(servico.Id))
                {
                    ToastHelper.Error(TempData, "Serviço não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                var temAgendamentos = await _context.Agendamentos
                    .AnyAsync(a => a.ServicoId == id);

                if (temAgendamentos && servico.Ativo)
                {
                    ToastHelper.Warning(TempData,
                        "Este serviço possui agendamentos vinculados e não pode ser inativado.");

                    return RedirectToAction(nameof(Index));
                }

                servico.Ativo = !servico.Ativo;

                await _context.SaveChangesAsync();

                ToastHelper.Success(
                    TempData,
                    servico.Ativo
                        ? "Serviço ativado com sucesso!"
                        : "Serviço inativado com sucesso!"
                );

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar status do serviço {ServicoId}.", id);
                ToastHelper.Error(TempData, "Erro ao alterar status.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}