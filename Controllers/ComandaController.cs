using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    // Comanda = os itens de produto consumidos durante um agendamento em
    // andamento. Não é uma tela separada de módulo — é operacional, aberta
    // por quem está atendendo (dono ou funcionário, sem exigir Gerente).
    [Route("agendamentos/{agendamentoId:int}/comanda")]
    [Authorize(Roles = "Empresa,Funcionario")]
    public class ComandaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IComandaService _comandaService;

        public ComandaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IComandaService comandaService)
        {
            _context = context;
            _userManager = userManager;
            _comandaService = comandaService;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        private async Task<int?> GetFuncionarioIdAsync()
        {
            if (!User.IsInRole("Funcionario"))
                return null;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return null;

            return await _context.Funcionarios
                .Where(f => f.UserId == user.Id)
                .Select(f => (int?)f.Id)
                .FirstOrDefaultAsync();
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int agendamentoId)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                var funcionarioId = await GetFuncionarioIdAsync();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var agendamento = await _context.Agendamentos
                    .Include(a => a.Cliente)
                    .Include(a => a.Servico)
                    .Include(a => a.ItensComanda).ThenInclude(i => i.Produto)
                    .FirstOrDefaultAsync(a =>
                        a.Id == agendamentoId &&
                        a.EmpresaId == empresaId &&
                        (funcionarioId == null || a.FuncionarioId == funcionarioId));

                if (agendamento == null)
                {
                    ToastHelper.Error(TempData, "Agendamento não encontrado.");
                    return RedirectToAction("Index", "Agendamentos");
                }

                ViewBag.ProdutosDisponiveis = await _context.Produtos
                    .Where(p => p.EmpresaId == empresaId && p.Ativo && p.QuantidadeEstoque > 0)
                    .OrderBy(p => p.Nome)
                    .ToListAsync();

                return View(agendamento);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar comanda.");
                return RedirectToAction("Index", "Agendamentos");
            }
        }

        [HttpPost("adicionar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(int agendamentoId, int produtoId, int quantidade)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var (sucesso, erro) = await _comandaService.AdicionarItemAsync(
                    empresaId.Value, agendamentoId, produtoId, quantidade);

                if (sucesso)
                    ToastHelper.Success(TempData, "Produto adicionado à comanda.");
                else
                    ToastHelper.Error(TempData, erro ?? "Não foi possível adicionar o produto.");

                return RedirectToAction(nameof(Index), new { agendamentoId });
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao adicionar produto à comanda.");
                return RedirectToAction(nameof(Index), new { agendamentoId });
            }
        }

        [HttpPost("{itemId:int}/remover")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remover(int agendamentoId, int itemId)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var (sucesso, erro) = await _comandaService.RemoverItemAsync(empresaId.Value, itemId);

                if (sucesso)
                    ToastHelper.Success(TempData, "Item removido da comanda.");
                else
                    ToastHelper.Error(TempData, erro ?? "Não foi possível remover o item.");

                return RedirectToAction(nameof(Index), new { agendamentoId });
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao remover item da comanda.");
                return RedirectToAction(nameof(Index), new { agendamentoId });
            }
        }
    }
}
