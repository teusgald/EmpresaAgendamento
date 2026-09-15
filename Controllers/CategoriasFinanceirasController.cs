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
    [Route("financeiro/categorias")]
    [Authorize(Roles = "Empresa")]
    [TypeFilter(typeof(RequerPlanoFinanceiroFilter))]
    public class CategoriasFinanceirasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoriasFinanceirasController(
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
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var lista = await _context.CategoriasFinanceiras
                .Where(c => c.EmpresaId == empresaId)
                .OrderBy(c => c.Tipo)
                .ThenBy(c => c.Nome)
                .ToListAsync();

            return View(lista);
        }

        // =========================
        // CREATE GET
        // =========================
        [HttpGet("nova")]
        public IActionResult Create()
        {
            return View();
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost("nova")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoriaFinanceira categoria)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            ModelState.Remove(nameof(CategoriaFinanceira.EmpresaId));
            ModelState.Remove(nameof(CategoriaFinanceira.Empresa));

            if (!ModelState.IsValid)
            {
                ToastHelper.Warning(TempData, "Preencha todos os campos corretamente.");
                return View(categoria);
            }

            var jaExiste = await _context.CategoriasFinanceiras
                .AnyAsync(c =>
                    c.EmpresaId == empresaId &&
                    c.Tipo == categoria.Tipo &&
                    c.Nome == categoria.Nome);

            if (jaExiste)
            {
                ToastHelper.Warning(TempData, "Já existe uma categoria com esse nome para esse tipo.");
                return View(categoria);
            }

            categoria.EmpresaId = empresaId.Value;
            categoria.Ativo = true;
            categoria.Padrao = false;
            categoria.DataCadastro = DateTime.UtcNow;

            _context.CategoriasFinanceiras.Add(categoria);
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Categoria cadastrada com sucesso!");

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // EDIT GET
        // =========================
        [HttpGet("editar/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var categoria = await _context.CategoriasFinanceiras
                .FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId);

            if (categoria == null)
            {
                ToastHelper.Error(TempData, "Categoria não encontrada.");
                return RedirectToAction(nameof(Index));
            }

            return View(categoria);
        }

        // =========================
        // EDIT POST
        // =========================
        [HttpPost("editar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoriaFinanceira categoria)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            if (id != categoria.Id)
            {
                ToastHelper.Error(TempData, "Requisição inválida.");
                return RedirectToAction(nameof(Index));
            }

            var existente = await _context.CategoriasFinanceiras
                .FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId);

            if (existente == null)
            {
                ToastHelper.Error(TempData, "Categoria não encontrada.");
                return RedirectToAction(nameof(Index));
            }

            // Categorias padrão (criadas automaticamente pelo financeiro, ex.:
            // "Serviços"/"Comissões") têm Nome/Tipo travados: o financeiro
            // procura por esse nome exato pra reaproveitar a categoria, e
            // renomear quebraria esse vínculo.
            if (!existente.Padrao)
            {
                if (string.IsNullOrWhiteSpace(categoria.Nome))
                {
                    ToastHelper.Warning(TempData, "Informe um nome válido.");
                    return View(existente);
                }

                existente.Nome = categoria.Nome;
                existente.Tipo = categoria.Tipo;
            }

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Categoria atualizada com sucesso!");

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // TOGGLE ATIVO
        // =========================
        [HttpPost("toggle-ativo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var categoria = await _context.CategoriasFinanceiras
                .FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId);

            if (categoria == null)
            {
                ToastHelper.Error(TempData, "Categoria não encontrada.");
                return RedirectToAction(nameof(Index));
            }

            categoria.Ativo = !categoria.Ativo;

            await _context.SaveChangesAsync();

            ToastHelper.Success(
                TempData,
                categoria.Ativo ? "Categoria ativada com sucesso!" : "Categoria inativada com sucesso!");

            return RedirectToAction(nameof(Index));
        }
    }
}
