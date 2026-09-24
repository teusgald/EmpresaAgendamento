using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class SimpliAiToolsService : ISimpliAiToolsService
    {
        private readonly ApplicationDbContext _context;

        public SimpliAiToolsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<object> ObterResumoAgendamentosAsync(int empresaId, DateTime inicio, DateTime fim)
        {
            var fimExclusivo = fim.Date.AddDays(1);

            var totalAgendamentos = await _context.Agendamentos
                .CountAsync(a =>
                    a.EmpresaId == empresaId &&
                    a.Ativo &&
                    a.DataHora >= inicio.Date &&
                    a.DataHora < fimExclusivo);

            var faturamento = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a =>
                    a.EmpresaId == empresaId &&
                    a.Status == StatusAgendamento.Finalizado &&
                    a.DataHora >= inicio.Date &&
                    a.DataHora < fimExclusivo)
                .SumAsync(a => (decimal?)a.Servico.Preco) ?? 0;

            var servicosPopulares = await _context.Agendamentos
                .Include(a => a.Servico)
                .Where(a =>
                    a.EmpresaId == empresaId &&
                    a.DataHora >= inicio.Date &&
                    a.DataHora < fimExclusivo)
                .GroupBy(a => a.Servico.Nome)
                .Select(g => new { Servico = g.Key, Quantidade = g.Count() })
                .OrderByDescending(x => x.Quantidade)
                .Take(5)
                .ToListAsync();

            return new
            {
                periodo = new { inicio = inicio.ToString("dd/MM/yyyy"), fim = fim.ToString("dd/MM/yyyy") },
                totalAgendamentos,
                faturamentoServicosFinalizados = faturamento,
                servicosMaisAgendados = servicosPopulares
            };
        }

        public async Task<object> ObterResumoFinanceiroAsync(int empresaId, DateTime inicio, DateTime fim)
        {
            var fimExclusivo = fim.Date.AddDays(1);

            var receitas = await _context.MovimentacoesFinanceiras
                .Where(m =>
                    m.EmpresaId == empresaId &&
                    m.Origem == OrigemMovimentacao.ContaReceber &&
                    m.DataMovimento >= inicio.Date &&
                    m.DataMovimento < fimExclusivo)
                .SumAsync(m => m.Tipo == TipoMovimentacao.Entrada ? m.Valor : -m.Valor);

            var despesas = await _context.MovimentacoesFinanceiras
                .Where(m =>
                    m.EmpresaId == empresaId &&
                    m.Origem == OrigemMovimentacao.ContaPagar &&
                    m.DataMovimento >= inicio.Date &&
                    m.DataMovimento < fimExclusivo)
                .SumAsync(m => m.Tipo == TipoMovimentacao.Saida ? m.Valor : -m.Valor);

            var contasAReceberAberto = await _context.ContasReceber
                .Where(c =>
                    c.EmpresaId == empresaId &&
                    (c.Status == StatusConta.Pendente || c.Status == StatusConta.Parcial))
                .SumAsync(c => (decimal?)(c.ValorPrevisto - c.ValorRecebido)) ?? 0;

            var contasAPagarAberto = await _context.ContasPagar
                .Where(c =>
                    c.EmpresaId == empresaId &&
                    (c.Status == StatusConta.Pendente || c.Status == StatusConta.Parcial))
                .SumAsync(c => (decimal?)(c.ValorPrevisto - c.ValorPago)) ?? 0;

            return new
            {
                periodo = new { inicio = inicio.ToString("dd/MM/yyyy"), fim = fim.ToString("dd/MM/yyyy") },
                receitas,
                despesas,
                lucro = receitas - despesas,
                contasAReceberEmAberto = contasAReceberAberto,
                contasAPagarEmAberto = contasAPagarAberto
            };
        }
    }
}
