using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("financeiro/contas-a-pagar")]
    [Authorize(Roles = "Empresa,Funcionario")]
    [TypeFilter(typeof(RequerGerenteFilter))]
    [TypeFilter(typeof(RequerPlanoFinanceiroFilter))]
    public class ContasPagarController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFinanceiroService _financeiroService;

        public ContasPagarController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFinanceiroService financeiroService)
        {
            _context = context;
            _userManager = userManager;
            _financeiroService = financeiroService;
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

        private async Task CarregarCombosAsync(int empresaId)
        {
            ViewBag.Categorias = new SelectList(
                await _context.CategoriasFinanceiras
                    .Where(c =>
                        c.EmpresaId == empresaId &&
                        c.Tipo == TipoCategoriaFinanceira.Despesa &&
                        c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");
        }

        // =========================
        // LISTAGEM (compartilhada por Contas a Pagar e Despesas)
        // =========================
        private async Task<IActionResult> ListarAsync(
            bool somenteAbertas,
            string titulo,
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            const int pageSize = 15;

            var query = _context.ContasPagar
                .Include(c => c.Categoria)
                .Include(c => c.Funcionario)
                .Where(c => c.EmpresaId == empresaId);

            if (somenteAbertas)
            {
                query = query.Where(c =>
                    c.Status == StatusConta.Pendente ||
                    c.Status == StatusConta.Parcial);
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            if (dataInicial.HasValue)
            {
                query = query.Where(c => c.DataVencimento >= dataInicial.Value);
            }

            if (dataFinal.HasValue)
            {
                query = query.Where(c => c.DataVencimento <= dataFinal.Value.AddDays(1));
            }

            query = query.OrderByDescending(c => c.DataVencimento);

            var totalItems = await query.CountAsync();

            var lista = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Titulo = titulo;
            ViewBag.SomenteAbertas = somenteAbertas;
            ViewBag.Status = status;
            ViewBag.DataInicial = dataInicial;
            ViewBag.DataFinal = dataFinal;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View("Index", lista);
        }

        // Contas a Pagar = em aberto (Pendente/Parcial)
        [HttpGet("")]
        public Task<IActionResult> Index(
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page = 1)
        {
            return ListarAsync(true, "Contas a Pagar", status, dataInicial, dataFinal, page);
        }

        // Despesas = histórico completo (todos os status)
        [HttpGet("/financeiro/despesas")]
        public Task<IActionResult> Despesas(
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page = 1)
        {
            return ListarAsync(false, "Despesas", status, dataInicial, dataFinal, page);
        }

        // =========================
        // CREATE (despesa avulsa) GET
        // =========================
        [HttpGet("nova")]
        public async Task<IActionResult> Create()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            await CarregarCombosAsync(empresaId.Value);

            return View(new ContaPagar
            {
                DataVencimento = DateTime.Today
            });
        }

        // =========================
        // CREATE (despesa avulsa) POST
        // =========================
        [HttpPost("nova")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContaPagar conta)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            ModelState.Remove(nameof(ContaPagar.EmpresaId));
            ModelState.Remove(nameof(ContaPagar.Empresa));
            ModelState.Remove(nameof(ContaPagar.Funcionario));
            ModelState.Remove(nameof(ContaPagar.Categoria));
            ModelState.Remove(nameof(ContaPagar.Status));
            ModelState.Remove(nameof(ContaPagar.ValorPago));

            if (conta.ValorPrevisto <= 0)
            {
                ModelState.AddModelError(
                    nameof(ContaPagar.ValorPrevisto),
                    "Informe um valor válido.");
            }

            // EmpresaId nunca vem do formulário: categoria informada é sempre
            // validada contra a empresa logada.
            if (conta.CategoriaId.HasValue)
            {
                var categoriaValida = await _context.CategoriasFinanceiras
                    .AnyAsync(c =>
                        c.Id == conta.CategoriaId &&
                        c.EmpresaId == empresaId);

                if (!categoriaValida)
                {
                    ModelState.AddModelError(
                        nameof(ContaPagar.CategoriaId),
                        "Categoria inválida.");
                }
            }

            if (!ModelState.IsValid)
            {
                await CarregarCombosAsync(empresaId.Value);
                return View(conta);
            }

            conta.EmpresaId = empresaId.Value;
            conta.FuncionarioId = null; // despesa manual não é pagamento de comissão
            conta.ValorPago = 0;
            conta.Status = StatusConta.Pendente;
            conta.DataCriacao = DateTime.UtcNow;

            _context.ContasPagar.Add(conta);
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Despesa lançada com sucesso!");

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // REGISTRAR PAGAMENTO (total ou parcial)
        // =========================
        [HttpPost("pagar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarPagamento(
            int id,
            decimal valor,
            FormaPagamento formaPagamento,
            string? origem)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            var usuarioId = _userManager.GetUserId(User);

            var (sucesso, erro, _) = await _financeiroService.RegistrarPagamentoAsync(
                id, empresaId.Value, valor, formaPagamento, usuarioId);

            if (!sucesso)
            {
                ToastHelper.Error(TempData, erro ?? "Erro ao registrar pagamento.");
            }
            else
            {
                ToastHelper.Success(TempData, "Pagamento registrado com sucesso!");
            }

            return RedirectToAction(origem == "despesas" ? nameof(Despesas) : nameof(Index));
        }

        // =========================
        // CANCELAR / ESTORNAR
        // =========================
        [HttpPost("cancelar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id, string motivo, string? origem)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = "Cancelado manualmente.";
            }

            var (sucesso, erro) = await _financeiroService.CancelarContaPagarAsync(
                id, empresaId.Value, motivo);

            if (!sucesso)
            {
                ToastHelper.Error(TempData, erro ?? "Erro ao cancelar.");
            }
            else
            {
                ToastHelper.Success(TempData, "Conta cancelada/estornada com sucesso.");
            }

            return RedirectToAction(origem == "despesas" ? nameof(Despesas) : nameof(Index));
        }
    }
}
