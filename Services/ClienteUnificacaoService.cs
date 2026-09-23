using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Services
{
    public class ClienteUnificacaoService : IClienteUnificacaoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClienteUnificacaoService> _logger;

        public ClienteUnificacaoService(ApplicationDbContext context, ILogger<ClienteUnificacaoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Cliente?> UnificarOrfaosAsync(string email)
        {
            // Sobrevivente = cadastro mais antigo — é a empresa que conhece
            // essa pessoa há mais tempo na plataforma.
            var orfaos = await _context.Clientes
                .Where(c => c.Email == email && c.UserId == null)
                .OrderBy(c => c.DataCadastro)
                .ToListAsync();

            if (orfaos.Count == 0)
                return null;

            var sobrevivente = orfaos[0];

            if (orfaos.Count == 1)
                return sobrevivente;

            var duplicados = orfaos.Skip(1).ToList();
            var idsDuplicados = duplicados.Select(d => d.Id).ToList();

            _logger.LogInformation(
                "Unificando {Total} cadastros de cliente órfãos pro e-mail {Email} — sobrevivente Id {SobreviventeId}, duplicados {Duplicados}.",
                orfaos.Count, email, sobrevivente.Id, string.Join(",", idsDuplicados));

            using var transacao = await _context.Database.BeginTransactionAsync();

            try
            {
                // ── VÍNCULO COM EMPRESA (EmpresaCliente) ──
                // Chave composta (EmpresaId, ClienteId): se a mesma empresa já
                // tem vínculo com o sobrevivente, só descarta o duplicado;
                // senão, repointa pro sobrevivente.
                var empresasDoSobrevivente = (await _context.EmpresaClientes
                    .Where(ec => ec.ClienteId == sobrevivente.Id)
                    .Select(ec => ec.EmpresaId)
                    .ToListAsync())
                    .ToHashSet();

                var vinculosDuplicados = await _context.EmpresaClientes
                    .Where(ec => idsDuplicados.Contains(ec.ClienteId))
                    .ToListAsync();

                foreach (var vinculo in vinculosDuplicados)
                {
                    if (empresasDoSobrevivente.Contains(vinculo.EmpresaId))
                    {
                        _context.EmpresaClientes.Remove(vinculo);
                    }
                    else
                    {
                        vinculo.ClienteId = sobrevivente.Id;
                        empresasDoSobrevivente.Add(vinculo.EmpresaId);
                    }
                }

                // ── AVALIAÇÕES ──
                // Índice único (EmpresaId, ClienteId) — mantém a mais recente
                // quando os dois cadastros avaliaram a mesma empresa.
                var avaliacoesSobrevivente = await _context.Avaliacoes
                    .Where(a => a.ClienteId == sobrevivente.Id)
                    .ToListAsync();

                var avaliacoesDuplicadas = await _context.Avaliacoes
                    .Where(a => idsDuplicados.Contains(a.ClienteId))
                    .ToListAsync();

                foreach (var avaliacao in avaliacoesDuplicadas)
                {
                    var existente = avaliacoesSobrevivente.FirstOrDefault(a => a.EmpresaId == avaliacao.EmpresaId);

                    if (existente == null)
                    {
                        avaliacao.ClienteId = sobrevivente.Id;
                        avaliacoesSobrevivente.Add(avaliacao);
                        continue;
                    }

                    var dataAvaliacao = avaliacao.DataAtualizacao ?? avaliacao.DataCriacao;
                    var dataExistente = existente.DataAtualizacao ?? existente.DataCriacao;

                    if (dataAvaliacao > dataExistente)
                    {
                        _context.Avaliacoes.Remove(existente);
                        avaliacoesSobrevivente.Remove(existente);
                        avaliacao.ClienteId = sobrevivente.Id;
                        avaliacoesSobrevivente.Add(avaliacao);
                    }
                    else
                    {
                        _context.Avaliacoes.Remove(avaliacao);
                    }
                }

                // ── FIDELIDADE ──
                // Índice único (ProgramaFidelidadeId, ClienteId) — soma o
                // progresso em vez de descartar (ninguém perde visita
                // contada por causa de cadastro duplicado).
                var fidelidadeSobrevivente = await _context.FidelidadeClientes
                    .Where(f => f.ClienteId == sobrevivente.Id)
                    .ToListAsync();

                var fidelidadeDuplicada = await _context.FidelidadeClientes
                    .Where(f => idsDuplicados.Contains(f.ClienteId))
                    .ToListAsync();

                foreach (var fidelidade in fidelidadeDuplicada)
                {
                    var existente = fidelidadeSobrevivente
                        .FirstOrDefault(f => f.ProgramaFidelidadeId == fidelidade.ProgramaFidelidadeId);

                    if (existente == null)
                    {
                        fidelidade.ClienteId = sobrevivente.Id;
                        fidelidadeSobrevivente.Add(fidelidade);
                        continue;
                    }

                    existente.VisitasContadas += fidelidade.VisitasContadas;
                    existente.DescontosDisponiveis += fidelidade.DescontosDisponiveis;
                    existente.DescontosUsados += fidelidade.DescontosUsados;

                    if (fidelidade.DataUltimaAtualizacao.HasValue &&
                        (!existente.DataUltimaAtualizacao.HasValue || fidelidade.DataUltimaAtualizacao > existente.DataUltimaAtualizacao))
                    {
                        existente.DataUltimaAtualizacao = fidelidade.DataUltimaAtualizacao;
                    }

                    _context.FidelidadeClientes.Remove(fidelidade);
                }

                // Salva as mudanças rastreadas até aqui antes dos updates em
                // massa abaixo (ExecuteUpdateAsync não passa pelo change
                // tracker, então não tem ordem garantida com o resto).
                await _context.SaveChangesAsync();

                // ── ASSINATURAS DE PLANO — sem índice único, repointing direto ──
                await _context.AssinaturasPlanoServico
                    .Where(a => idsDuplicados.Contains(a.ClienteId))
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClienteId, sobrevivente.Id));

                // ── AGENDAMENTOS — repointing direto ──
                await _context.Agendamentos
                    .Where(a => a.ClienteId != null && idsDuplicados.Contains(a.ClienteId!.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClienteId, sobrevivente.Id));

                // ── CONTAS A RECEBER — repointing direto ──
                await _context.ContasReceber
                    .Where(c => c.ClienteId != null && idsDuplicados.Contains(c.ClienteId!.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClienteId, sobrevivente.Id));

                // ── NOTIFICAÇÕES — repointing direto ──
                await _context.Notificacoes
                    .Where(n => n.ClienteId != null && idsDuplicados.Contains(n.ClienteId!.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.ClienteId, sobrevivente.Id));

                // ── AspNetUsers.ClienteId ──
                // Não é uma FK de verdade no banco (sem índice/constraint),
                // mas é lida manualmente em vários pontos do código — corrige
                // qualquer referência solta apontando pra um duplicado.
                var usuariosComReferenciaSolta = await _context.Users
                    .Where(u => u.ClienteId != null && idsDuplicados.Contains(u.ClienteId.Value))
                    .ToListAsync();

                foreach (var usuario in usuariosComReferenciaSolta)
                {
                    usuario.ClienteId = sobrevivente.Id;
                }

                // ── APAGA OS CADASTROS DUPLICADOS ──
                _context.Clientes.RemoveRange(duplicados);

                await _context.SaveChangesAsync();
                await transacao.CommitAsync();
            }
            catch
            {
                await transacao.RollbackAsync();
                throw;
            }

            return sobrevivente;
        }
    }
}
