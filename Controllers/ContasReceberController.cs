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
    [Route("financeiro/contas-a-receber")]
    [Authorize(Roles = "Empresa")]
    [TypeFilter(typeof(RequerPlanoFinanceiroFilter))]
    public class ContasReceberController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFinanceiroService _financeiroService;

        public ContasReceberController(
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
                        c.Tipo == TipoCategoriaFinanceira.Receita &&
                        c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");

            ViewBag.Clientes = new SelectList(
                await _context.Clientes
                    .Where(c =>
                        c.Ativo &&
                        c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");
        }

        // =========================
        // LISTAGEM (compartilhada por Contas a Receber e Receitas)
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

            var query = _context.ContasReceber
                .Include(c => c.Cliente)
                .Include(c => c.Categoria)
                .Include(c => c.Agendamento)
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

        // Contas a Receber = em aberto (Pendente/Parcial)
        [HttpGet("")]
        public Task<IActionResult> Index(
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page = 1)
        {
            return ListarAsync(true, "Contas a Receber", status, dataInicial, dataFinal, page);
        }

        // Receitas = histórico completo (todos os status)
        [HttpGet("/financeiro/receitas")]
        public Task<IActionResult> Receitas(
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page = 1)
        {
            return ListarAsync(false, "Receitas", status, dataInicial, dataFinal, page);
        }

        // =========================
        // CREATE (receita avulsa) GET
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

            return View(new ContaReceber
            {
                DataVencimento = DateTime.Today
            });
        }

        // =========================
        // CREATE (receita avulsa) POST
        // =========================
        [HttpPost("nova")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContaReceber conta)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "EmpresaAuth");
            }

            ModelState.Remove(nameof(ContaReceber.EmpresaId));
            ModelState.Remove(nameof(ContaReceber.Empresa));
            ModelState.Remove(nameof(ContaReceber.Agendamento));
            ModelState.Remove(nameof(ContaReceber.Cliente));
            ModelState.Remove(nameof(ContaReceber.Categoria));
            ModelState.Remove(nameof(ContaReceber.Status));
            ModelState.Remove(nameof(ContaReceber.ValorRecebido));

            if (conta.ValorPrevisto <= 0)
            {
                ModelState.AddModelError(
                    nameof(ContaReceber.ValorPrevisto),
                    "Informe um valor válido.");
            }

            // EmpresaId nunca vem do formulário: toda referência informada
            // (Categoria/Cliente) é validada contra a empresa logada.
            if (conta.CategoriaId.HasValue)
            {
                var categoriaValida = await _context.CategoriasFinanceiras
                    .AnyAsync(c =>
                        c.Id == conta.CategoriaId &&
                        c.EmpresaId == empresaId);

                if (!categoriaValida)
                {
                    ModelState.AddModelError(
                        nameof(ContaReceber.CategoriaId),
                        "Categoria inválida.");
                }
            }

            if (conta.ClienteId.HasValue)
            {
                var clienteValido = await _context.Clientes
                    .AnyAsync(c =>
                        c.Id == conta.ClienteId &&
                        c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

                if (!clienteValido)
                {
                    ModelState.AddModelError(
                        nameof(ContaReceber.ClienteId),
                        "Cliente inválido.");
                }
            }

            if (!ModelState.IsValid)
            {
                await CarregarCombosAsync(empresaId.Value);
                return View(conta);
            }

            conta.EmpresaId = empresaId.Value;
            conta.AgendamentoId = null;
            conta.ValorRecebido = 0;
            conta.Status = StatusConta.Pendente;
            conta.DataCriacao = DateTime.UtcNow;

            _context.ContasReceber.Add(conta);
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Receita lançada com sucesso!");

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // REGISTRAR RECEBIMENTO (total ou parcial)
        // =========================
        [HttpPost("receber")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarRecebimento(
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

            var (sucesso, erro, _) = await _financeiroService.RegistrarRecebimentoAsync(
                id, empresaId.Value, valor, formaPagamento, usuarioId);

            if (!sucesso)
            {
                ToastHelper.Error(TempData, erro ?? "Erro ao registrar recebimento.");
            }
            else
            {
                ToastHelper.Success(TempData, "Recebimento registrado com sucesso!");
            }

            return RedirectToAction(origem == "receitas" ? nameof(Receitas) : nameof(Index));
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

            var (sucesso, erro) = await _financeiroService.CancelarContaReceberAsync(
                id, empresaId.Value, motivo);

            if (!sucesso)
            {
                ToastHelper.Error(TempData, erro ?? "Erro ao cancelar.");
            }
            else
            {
                ToastHelper.Success(TempData, "Conta cancelada/estornada com sucesso.");
            }

            return RedirectToAction(origem == "receitas" ? nameof(Receitas) : nameof(Index));
        }
    }
}
