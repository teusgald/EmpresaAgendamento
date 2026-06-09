using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Authorize(Roles = "Empresa")]
    public class ServicosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ServicosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

        // =========================
        // INDEX
        // =========================
        [HttpGet("/Servicos")]
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

                var query = _context.Servicos
                    .Where(s => s.EmpresaId == empresaId)
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar serviços.");
                return RedirectToAction("Index", "Home");
            }
        }

        // =========================
        // CREATE GET
        // =========================
        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                return View();
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao abrir formulário.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao criar serviço.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // EDIT GET
        // =========================
        [HttpGet]
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

                return View(servico);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar serviço.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // EDIT POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                existente.Nome = servico.Nome;
                existente.Preco = servico.Preco;
                existente.DuracaoMinutos = servico.DuracaoMinutos;

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Serviço atualizado com sucesso!");

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao atualizar serviço.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // TOGGLE ATIVO
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
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
            catch
            {
                ToastHelper.Error(TempData, "Erro ao alterar status.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}