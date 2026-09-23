using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class ComandaService : IComandaService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFinanceiroService _financeiroService;

        public ComandaService(ApplicationDbContext context, IFinanceiroService financeiroService)
        {
            _context = context;
            _financeiroService = financeiroService;
        }

        public async Task<(bool Sucesso, string? Erro)> AdicionarItemAsync(
            int empresaId, int agendamentoId, int produtoId, int quantidade)
        {
            if (quantidade <= 0)
                return (false, "Informe uma quantidade válida.");

            var agendamento = await _context.Agendamentos
                .FirstOrDefaultAsync(a => a.Id == agendamentoId && a.EmpresaId == empresaId);

            if (agendamento == null)
                return (false, "Agendamento não encontrado.");

            if (await ContaJaQuitadaAsync(agendamentoId))
                return (false, "Essa comanda já foi paga — não é possível adicionar mais itens.");

            var produto = await _context.Produtos
                .FirstOrDefaultAsync(p => p.Id == produtoId && p.EmpresaId == empresaId && p.Ativo);

            if (produto == null)
                return (false, "Produto não encontrado.");

            if (produto.QuantidadeEstoque < quantidade)
                return (false, $"Estoque insuficiente — restam {produto.QuantidadeEstoque} unidade(s).");

            produto.QuantidadeEstoque -= quantidade;

            _context.ItensComanda.Add(new ItemComanda
            {
                AgendamentoId = agendamentoId,
                ProdutoId = produtoId,
                Quantidade = quantidade,
                PrecoUnitario = produto.Preco
            });

            await _context.SaveChangesAsync();
            await _financeiroService.AtualizarValorContaReceberDeAgendamentoAsync(agendamentoId);

            return (true, null);
        }

        public async Task<(bool Sucesso, string? Erro)> RemoverItemAsync(int empresaId, int itemComandaId)
        {
            var item = await _context.ItensComanda
                .Include(i => i.Agendamento)
                .Include(i => i.Produto)
                .FirstOrDefaultAsync(i => i.Id == itemComandaId && i.Agendamento.EmpresaId == empresaId);

            if (item == null)
                return (false, "Item não encontrado.");

            if (await ContaJaQuitadaAsync(item.AgendamentoId))
                return (false, "Essa comanda já foi paga — não é possível remover itens.");

            item.Produto.QuantidadeEstoque += item.Quantidade;

            var agendamentoId = item.AgendamentoId;

            _context.ItensComanda.Remove(item);
            await _context.SaveChangesAsync();
            await _financeiroService.AtualizarValorContaReceberDeAgendamentoAsync(agendamentoId);

            return (true, null);
        }

        private async Task<bool> ContaJaQuitadaAsync(int agendamentoId)
        {
            return await _context.ContasReceber
                .Where(c => c.AgendamentoId == agendamentoId)
                .Select(c => c.Status == StatusConta.Quitado)
                .FirstOrDefaultAsync();
        }
    }
}
