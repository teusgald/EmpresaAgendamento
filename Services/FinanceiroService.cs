using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class FinanceiroService : IFinanceiroService
    {
        private readonly ApplicationDbContext _context;

        public FinanceiroService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // CATEGORIAS PADRÃO
        // =========================

        private async Task<CategoriaFinanceira> ObterOuCriarCategoriaPadraoAsync(
            int empresaId, TipoCategoriaFinanceira tipo, string nome)
        {
            var categoria = await _context.CategoriasFinanceiras
                .FirstOrDefaultAsync(c =>
                    c.EmpresaId == empresaId &&
                    c.Tipo == tipo &&
                    c.Nome == nome);

            if (categoria != null)
                return categoria;

            categoria = new CategoriaFinanceira
            {
                EmpresaId = empresaId,
                Nome = nome,
                Tipo = tipo,
                Padrao = true,
                Ativo = true
            };

            _context.CategoriasFinanceiras.Add(categoria);
            await _context.SaveChangesAsync();

            return categoria;
        }

        // =========================
        // INTEGRAÇÃO COM AGENDAMENTO
        // =========================

        public async Task<ContaReceber?> GerarContaReceberDeAgendamentoAsync(int agendamentoId)
        {
            // Idempotência: se já existe conta a receber pra esse agendamento,
            // não gera outra (também travado por índice único no banco).
            var existente = await _context.ContasReceber
                .FirstOrDefaultAsync(c => c.AgendamentoId == agendamentoId);

            if (existente != null)
                return existente;

            var agendamento = await _context.Agendamentos
                .Include(a => a.Servico)
                .Include(a => a.Funcionario)
                .FirstOrDefaultAsync(a => a.Id == agendamentoId);

            if (agendamento == null)
                return null;

            var categoria = await ObterOuCriarCategoriaPadraoAsync(
                agendamento.EmpresaId,
                TipoCategoriaFinanceira.Receita,
                "Serviços");

            var conta = new ContaReceber
            {
                EmpresaId = agendamento.EmpresaId,
                AgendamentoId = agendamento.Id,
                ClienteId = agendamento.ClienteId,
                CategoriaId = categoria.Id,
                Descricao = $"Serviço: {agendamento.Servico.Nome}",
                ValorPrevisto = agendamento.Servico.Preco,
                ValorRecebido = 0,
                DataVencimento = agendamento.DataHora,
                Status = StatusConta.Pendente
            };

            _context.ContasReceber.Add(conta);

            await _context.SaveChangesAsync();

            return conta;
        }

        // Calcula e registra a comissão do funcionário responsável pelo
        // agendamento, reaproveitando AgendamentoFuncionario (nunca populado
        // até então). Não paga a comissão — só a apura; o pagamento em si é
        // feito depois, no módulo de Comissões.
        //
        // Chamado apenas quando o agendamento é Finalizado (a comissão só faz
        // sentido sobre serviço efetivamente realizado — diferente da conta a
        // receber, que já existe desde a criação do agendamento).
        public async Task ApurarComissaoDoAgendamentoAsync(int agendamentoId)
        {
            var agendamento = await _context.Agendamentos
                .Include(a => a.Servico)
                .Include(a => a.Funcionario)
                .FirstOrDefaultAsync(a => a.Id == agendamentoId);

            if (agendamento == null)
                return;

            if (!agendamento.FuncionarioId.HasValue)
                return;

            var jaExiste = await _context.AgendamentosFuncionarios
                .AnyAsync(x =>
                    x.AgendamentoId == agendamento.Id &&
                    x.FuncionarioId == agendamento.FuncionarioId.Value);

            if (jaExiste)
                return;

            var funcionario = agendamento.Funcionario
                ?? await _context.Funcionarios.FindAsync(agendamento.FuncionarioId.Value);

            if (funcionario == null)
                return;

            decimal? percentual = funcionario.PercentualComissaoPadrao;
            decimal? valorComissao = null;

            if (percentual.HasValue && percentual.Value > 0)
            {
                valorComissao = Math.Round(
                    agendamento.Servico.Preco * (percentual.Value / 100m), 2);
            }
            else if (funcionario.ValorComissaoFixa.HasValue && funcionario.ValorComissaoFixa.Value > 0)
            {
                valorComissao = funcionario.ValorComissaoFixa.Value;
            }

            var vinculo = new AgendamentoFuncionario
            {
                AgendamentoId = agendamento.Id,
                FuncionarioId = funcionario.Id,
                ResponsavelPrincipal = true,
                PercentualComissao = percentual,
                ValorComissao = valorComissao
            };

            _context.AgendamentosFuncionarios.Add(vinculo);

            await _context.SaveChangesAsync();
        }

        public async Task CancelarContaReceberDeAgendamentoAsync(int agendamentoId, string motivo)
        {
            var conta = await _context.ContasReceber
                .FirstOrDefaultAsync(c => c.AgendamentoId == agendamentoId);

            if (conta == null)
                return;

            if (conta.Status == StatusConta.Cancelado || conta.Status == StatusConta.Estornado)
                return;

            await CancelarContaReceberInternoAsync(conta, motivo);
        }

        // =========================
        // SINCRONIZAÇÃO (BACKFILL)
        // =========================

        public async Task<int> SincronizarAgendamentosExistentesAsync(int empresaId)
        {
            var idsComConta = await _context.ContasReceber
                .Where(c => c.EmpresaId == empresaId && c.AgendamentoId != null)
                .Select(c => c.AgendamentoId!.Value)
                .ToListAsync();

            var pendentes = await _context.Agendamentos
                .Where(a => a.EmpresaId == empresaId && !idsComConta.Contains(a.Id))
                .Select(a => new { a.Id, a.Status })
                .ToListAsync();

            var processados = 0;

            foreach (var item in pendentes)
            {
                var conta = await GerarContaReceberDeAgendamentoAsync(item.Id);

                if (conta == null)
                    continue;

                if (item.Status == StatusAgendamento.Cancelado)
                {
                    await CancelarContaReceberDeAgendamentoAsync(
                        item.Id,
                        "Agendamento já estava cancelado (sincronização).");
                }
                else if (item.Status == StatusAgendamento.Finalizado)
                {
                    await ApurarComissaoDoAgendamentoAsync(item.Id);
                }

                processados++;
            }

            return processados;
        }

        // =========================
        // RECEBIMENTOS
        // =========================

        public async Task<(bool Sucesso, string? Erro, ContaReceber? Conta)> RegistrarRecebimentoAsync(
            int contaReceberId,
            int empresaId,
            decimal valor,
            FormaPagamento formaPagamento,
            string? usuarioId,
            DateTime? dataRecebimento = null,
            string? referenciaExterna = null)
        {
            // EmpresaId sempre validado no backend contra o dono real da conta.
            var conta = await _context.ContasReceber
                .FirstOrDefaultAsync(c => c.Id == contaReceberId && c.EmpresaId == empresaId);

            if (conta == null)
                return (false, "Conta a receber não encontrada.", null);

            if (conta.Status == StatusConta.Cancelado || conta.Status == StatusConta.Estornado)
                return (false, "Esta conta está cancelada/estornada.", conta);

            if (conta.Status == StatusConta.Quitado)
                return (false, "Esta conta já está quitada.", conta);

            if (valor <= 0)
                return (false, "Informe um valor de recebimento válido.", conta);

            var saldoRestante = conta.ValorPrevisto - conta.ValorRecebido;

            if (valor > saldoRestante)
                return (false, "O valor informado é maior que o saldo em aberto.", conta);

            var dataEfetiva = dataRecebimento ?? DateTime.UtcNow;

            conta.ValorRecebido += valor;
            conta.FormaPagamento = formaPagamento;
            conta.DataRecebimento = dataEfetiva;

            conta.Status = conta.ValorRecebido >= conta.ValorPrevisto
                ? StatusConta.Quitado
                : StatusConta.Parcial;

            var movimentacao = new MovimentacaoFinanceira
            {
                EmpresaId = empresaId,
                Tipo = TipoMovimentacao.Entrada,
                Origem = OrigemMovimentacao.ContaReceber,
                Valor = valor,
                FormaPagamento = formaPagamento,
                DataMovimento = dataEfetiva,
                Status = StatusMovimentacao.Confirmada,
                Descricao = conta.Descricao,
                ContaReceberId = conta.Id,
                CategoriaId = conta.CategoriaId,
                UsuarioId = usuarioId,
                ReferenciaExterna = referenciaExterna
            };

            _context.MovimentacoesFinanceiras.Add(movimentacao);

            await _context.SaveChangesAsync();

            return (true, null, conta);
        }

        public async Task<(bool Sucesso, string? Erro)> CancelarContaReceberAsync(
            int contaReceberId,
            int empresaId,
            string motivo)
        {
            var conta = await _context.ContasReceber
                .FirstOrDefaultAsync(c => c.Id == contaReceberId && c.EmpresaId == empresaId);

            if (conta == null)
                return (false, "Conta a receber não encontrada.");

            if (conta.Status == StatusConta.Cancelado || conta.Status == StatusConta.Estornado)
                return (false, "Esta conta já está cancelada/estornada.");

            await CancelarContaReceberInternoAsync(conta, motivo);

            return (true, null);
        }

        // Cancela sem histórico de recebimento; estorna (gera movimentação de
        // sinal contrário, preservando a original) quando já houve recebimento
        // confirmado. Nunca exclui a conta nem a movimentação.
        private async Task CancelarContaReceberInternoAsync(ContaReceber conta, string motivo)
        {
            var movimentacoesConfirmadas = await _context.MovimentacoesFinanceiras
                .Where(m =>
                    m.ContaReceberId == conta.Id &&
                    m.Status == StatusMovimentacao.Confirmada)
                .ToListAsync();

            foreach (var mov in movimentacoesConfirmadas)
            {
                var estorno = new MovimentacaoFinanceira
                {
                    EmpresaId = mov.EmpresaId,
                    Tipo = TipoMovimentacao.Saida,
                    Origem = OrigemMovimentacao.ContaReceber,
                    Valor = mov.Valor,
                    FormaPagamento = mov.FormaPagamento,
                    DataMovimento = DateTime.UtcNow,
                    Status = StatusMovimentacao.Confirmada,
                    Descricao = $"Estorno: {mov.Descricao}",
                    MovimentacaoOrigemEstornoId = mov.Id,
                    ContaReceberId = conta.Id,
                    CategoriaId = mov.CategoriaId
                };

                // A original fica marcada como Estornada (só pra sinalização/
                // filtro no extrato) — o saldo de caixa soma as duas linhas
                // (a original + o estorno de sinal contrário), então o efeito
                // líquido já é zero independente desse status.
                mov.Status = StatusMovimentacao.Estornada;

                _context.MovimentacoesFinanceiras.Add(estorno);
            }

            conta.Status = movimentacoesConfirmadas.Any()
                ? StatusConta.Estornado
                : StatusConta.Cancelado;

            conta.DataCancelamento = DateTime.UtcNow;
            conta.MotivoCancelamento = motivo;

            await _context.SaveChangesAsync();
        }

        // =========================
        // PAGAMENTOS (DESPESAS)
        // =========================

        public async Task<(bool Sucesso, string? Erro, ContaPagar? Conta)> RegistrarPagamentoAsync(
            int contaPagarId,
            int empresaId,
            decimal valor,
            FormaPagamento formaPagamento,
            string? usuarioId,
            DateTime? dataPagamento = null,
            string? referenciaExterna = null)
        {
            // EmpresaId sempre validado no backend contra o dono real da conta.
            var conta = await _context.ContasPagar
                .FirstOrDefaultAsync(c => c.Id == contaPagarId && c.EmpresaId == empresaId);

            if (conta == null)
                return (false, "Conta a pagar não encontrada.", null);

            if (conta.Status == StatusConta.Cancelado || conta.Status == StatusConta.Estornado)
                return (false, "Esta conta está cancelada/estornada.", conta);

            if (conta.Status == StatusConta.Quitado)
                return (false, "Esta conta já está quitada.", conta);

            if (valor <= 0)
                return (false, "Informe um valor de pagamento válido.", conta);

            var saldoRestante = conta.ValorPrevisto - conta.ValorPago;

            if (valor > saldoRestante)
                return (false, "O valor informado é maior que o saldo em aberto.", conta);

            var dataEfetiva = dataPagamento ?? DateTime.UtcNow;

            conta.ValorPago += valor;
            conta.FormaPagamento = formaPagamento;
            conta.DataPagamento = dataEfetiva;

            conta.Status = conta.ValorPago >= conta.ValorPrevisto
                ? StatusConta.Quitado
                : StatusConta.Parcial;

            var movimentacao = new MovimentacaoFinanceira
            {
                EmpresaId = empresaId,
                Tipo = TipoMovimentacao.Saida,
                Origem = OrigemMovimentacao.ContaPagar,
                Valor = valor,
                FormaPagamento = formaPagamento,
                DataMovimento = dataEfetiva,
                Status = StatusMovimentacao.Confirmada,
                Descricao = conta.Descricao,
                ContaPagarId = conta.Id,
                CategoriaId = conta.CategoriaId,
                UsuarioId = usuarioId,
                ReferenciaExterna = referenciaExterna
            };

            _context.MovimentacoesFinanceiras.Add(movimentacao);

            await _context.SaveChangesAsync();

            return (true, null, conta);
        }

        public async Task<(bool Sucesso, string? Erro)> CancelarContaPagarAsync(
            int contaPagarId,
            int empresaId,
            string motivo)
        {
            var conta = await _context.ContasPagar
                .FirstOrDefaultAsync(c => c.Id == contaPagarId && c.EmpresaId == empresaId);

            if (conta == null)
                return (false, "Conta a pagar não encontrada.");

            if (conta.Status == StatusConta.Cancelado || conta.Status == StatusConta.Estornado)
                return (false, "Esta conta já está cancelada/estornada.");

            await CancelarContaPagarInternoAsync(conta, motivo);

            return (true, null);
        }

        // Cancela sem histórico de pagamento; estorna (gera movimentação de
        // sinal contrário, preservando a original) quando já houve pagamento
        // confirmado. Nunca exclui a conta nem a movimentação.
        private async Task CancelarContaPagarInternoAsync(ContaPagar conta, string motivo)
        {
            var movimentacoesConfirmadas = await _context.MovimentacoesFinanceiras
                .Where(m =>
                    m.ContaPagarId == conta.Id &&
                    m.Status == StatusMovimentacao.Confirmada)
                .ToListAsync();

            foreach (var mov in movimentacoesConfirmadas)
            {
                var estorno = new MovimentacaoFinanceira
                {
                    EmpresaId = mov.EmpresaId,
                    Tipo = TipoMovimentacao.Entrada,
                    Origem = OrigemMovimentacao.ContaPagar,
                    Valor = mov.Valor,
                    FormaPagamento = mov.FormaPagamento,
                    DataMovimento = DateTime.UtcNow,
                    Status = StatusMovimentacao.Confirmada,
                    Descricao = $"Estorno: {mov.Descricao}",
                    MovimentacaoOrigemEstornoId = mov.Id,
                    ContaPagarId = conta.Id,
                    CategoriaId = mov.CategoriaId
                };

                mov.Status = StatusMovimentacao.Estornada;

                _context.MovimentacoesFinanceiras.Add(estorno);
            }

            conta.Status = movimentacoesConfirmadas.Any()
                ? StatusConta.Estornado
                : StatusConta.Cancelado;

            conta.DataCancelamento = DateTime.UtcNow;
            conta.MotivoCancelamento = motivo;

            await _context.SaveChangesAsync();
        }

        // =========================
        // COMISSÕES
        // =========================

        public async Task<(bool Sucesso, string? Erro, ContaPagar? Conta)> PagarComissoesPendentesAsync(
            int empresaId,
            int funcionarioId,
            DateTime? dataInicial,
            DateTime? dataFinal,
            FormaPagamento formaPagamento,
            string? usuarioId)
        {
            var funcionario = await _context.Funcionarios
                .FirstOrDefaultAsync(f => f.Id == funcionarioId && f.EmpresaId == empresaId);

            if (funcionario == null)
                return (false, "Funcionário não encontrado.", null);

            var query = _context.AgendamentosFuncionarios
                .Include(af => af.Agendamento)
                .Where(af =>
                    af.FuncionarioId == funcionarioId &&
                    af.Agendamento.EmpresaId == empresaId &&
                    af.ValorComissao != null &&
                    af.ValorComissao > 0 &&
                    af.ValorRecebido == null);

            if (dataInicial.HasValue)
                query = query.Where(af => af.Agendamento.DataHora >= dataInicial.Value);

            if (dataFinal.HasValue)
                query = query.Where(af => af.Agendamento.DataHora <= dataFinal.Value.AddDays(1));

            var pendentes = await query.ToListAsync();

            if (!pendentes.Any())
                return (false, "Nenhuma comissão pendente encontrada para esse período.", null);

            var total = pendentes.Sum(af => af.ValorComissao!.Value);

            var categoria = await ObterOuCriarCategoriaPadraoAsync(
                empresaId, TipoCategoriaFinanceira.Despesa, "Comissões");

            var dataEfetiva = DateTime.UtcNow;

            var conta = new ContaPagar
            {
                EmpresaId = empresaId,
                FuncionarioId = funcionarioId,
                CategoriaId = categoria.Id,
                Descricao = $"Comissão - {funcionario.Nome}",
                ValorPrevisto = total,
                ValorPago = total,
                FormaPagamento = formaPagamento,
                DataVencimento = dataEfetiva,
                DataPagamento = dataEfetiva,
                Status = StatusConta.Quitado
            };

            _context.ContasPagar.Add(conta);

            var movimentacao = new MovimentacaoFinanceira
            {
                EmpresaId = empresaId,
                Tipo = TipoMovimentacao.Saida,
                Origem = OrigemMovimentacao.ContaPagar,
                Valor = total,
                FormaPagamento = formaPagamento,
                DataMovimento = dataEfetiva,
                Status = StatusMovimentacao.Confirmada,
                Descricao = conta.Descricao,
                CategoriaId = categoria.Id,
                UsuarioId = usuarioId,
                ContaPagar = conta
            };

            _context.MovimentacoesFinanceiras.Add(movimentacao);

            foreach (var af in pendentes)
            {
                af.ValorRecebido = af.ValorComissao;
                af.ContaPagar = conta;
            }

            await _context.SaveChangesAsync();

            return (true, null, conta);
        }
    }
}
