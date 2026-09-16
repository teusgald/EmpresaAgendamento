using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Servico> Servicos { get; set; }
        public DbSet<Agendamento> Agendamentos { get; set; }

        // Novo DbSet para relacionamento muitos-para-muitos
        public DbSet<EmpresaCliente> EmpresaClientes { get; set; }

        public DbSet<Funcionario> Funcionarios { get; set; }

        public DbSet<FuncionarioHorario> FuncionariosHorarios { get; set; }

        public DbSet<FuncionarioServico> FuncionariosServicos { get; set; }

        public DbSet<AgendamentoFuncionario> AgendamentosFuncionarios { get; set; }

        public DbSet<Plano> Planos { get; set; }

        // Financeiro
        public DbSet<CategoriaFinanceira> CategoriasFinanceiras { get; set; }
        public DbSet<ContaReceber> ContasReceber { get; set; }
        public DbSet<ContaPagar> ContasPagar { get; set; }
        public DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras { get; set; }

        public DbSet<EmpresaFoto> EmpresaFotos { get; set; }
        public DbSet<Avaliacao> Avaliacoes { get; set; }

        public DbSet<PlanoServico> PlanosServico { get; set; }
        public DbSet<PlanoServicoItem> PlanosServicoItens { get; set; }
        public DbSet<AssinaturaPlanoServico> AssinaturasPlanoServico { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            #region Identity

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Empresa)
                .WithMany(e => e.Usuarios)
                .HasForeignKey(u => u.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasIndex(x => x.EmpresaId);

            builder.Entity<Cliente>()
                .HasOne(c => c.User)
                .WithOne(u => u.Cliente)
                .HasForeignKey<Cliente>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Cliente>()
    .HasIndex(x => x.UserId)
    .IsUnique();

            builder.Entity<Funcionario>()
                .HasOne(f => f.User)
                .WithOne(u => u.Funcionario)
                .HasForeignKey<Funcionario>(f => f.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            #endregion

            #region Empresa

            builder.Entity<Empresa>()
                .Property(e => e.Nome)
                .HasMaxLength(150)
                .IsRequired();

            builder.Entity<Empresa>()
                .Property(e => e.EmailContato)
                .HasMaxLength(150);

            builder.Entity<Empresa>()
                .HasIndex(e => e.Slug)
                .IsUnique();

            // Único só quando preenchido — o cadastro inicial não coleta
            // CPF/CNPJ, então várias empresas podem ficar sem ele por enquanto.
            builder.Entity<Empresa>()
                .HasIndex(e => e.DocumentoNumero)
                .IsUnique()
                .HasFilter("[DocumentoNumero] IS NOT NULL");

            builder.Entity<Empresa>()
    .Property(x => x.Latitude)
    .HasPrecision(9, 6);

            builder.Entity<Empresa>()
                .Property(x => x.Longitude)
                .HasPrecision(9, 6);

            builder.Entity<Empresa>()
                .HasOne(e => e.Plano)
                .WithMany(p => p.Empresas)
                .HasForeignKey(e => e.PlanoId)
                .OnDelete(DeleteBehavior.SetNull);

            #endregion

            #region Cliente

            builder.Entity<Cliente>()
                .Property(c => c.Nome)
                .HasMaxLength(150)
                .IsRequired();

            #endregion

            #region Serviço

            builder.Entity<Servico>()
                .Property(s => s.Nome)
                .HasMaxLength(150)
                .IsRequired();

            builder.Entity<Servico>()
                .Property(s => s.Preco)
                .HasPrecision(10, 2);

            builder.Entity<Servico>()
                .HasOne(s => s.Empresa)
                .WithMany(e => e.Servicos)
                .HasForeignKey(s => s.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Servico>()
    .HasIndex(x => x.EmpresaId);

            #endregion

            #region Agendamento

            builder.Entity<Agendamento>()
                .HasOne(a => a.Empresa)
                .WithMany(e => e.Agendamentos)
                .HasForeignKey(a => a.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Agendamento>()
                .HasOne(a => a.Cliente)
                .WithMany(c => c.Agendamentos)
                .HasForeignKey(a => a.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Agendamento>()
                .HasOne(a => a.Servico)
                .WithMany(s => s.Agendamentos)
                .HasForeignKey(a => a.ServicoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Agendamento>()
                .Property(a => a.Ativo)
                .HasDefaultValue(true);

            builder.Entity<Agendamento>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Entity<Agendamento>()
                .HasIndex(x => x.EmpresaId);

            builder.Entity<Agendamento>()
    .HasIndex(x => new
    {
        x.EmpresaId,
        x.DataHora
    });

            #endregion

            #region EmpresaCliente

            builder.Entity<EmpresaCliente>()
                .HasKey(x => new
                {
                    x.EmpresaId,
                    x.ClienteId
                });

            builder.Entity<EmpresaCliente>()
                .HasOne(x => x.Empresa)
                .WithMany(x => x.EmpresaClientes)
                .HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EmpresaCliente>()
                .HasOne(x => x.Cliente)
                .WithMany(x => x.EmpresaClientes)
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region Funcionários

            builder.Entity<Funcionario>()
                .HasOne(f => f.Empresa)
                .WithMany(e => e.Funcionarios)
                .HasForeignKey(f => f.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Funcionario>()
                .Property(x => x.PercentualComissaoPadrao)
                .HasPrecision(5, 2);

            builder.Entity<Funcionario>()
    .HasIndex(x => x.UserId)
    .IsUnique();

            builder.Entity<Funcionario>()
                .Property(x => x.ValorComissaoFixa)
                .HasPrecision(10, 2);

            builder.Entity<FuncionarioServico>()
                .HasKey(x => new
                {
                    x.FuncionarioId,
                    x.ServicoId
                });

            builder.Entity<FuncionarioServico>()
                .HasOne(x => x.Funcionario)
                .WithMany(x => x.Servicos)
                .HasForeignKey(x => x.FuncionarioId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FuncionarioServico>()
                .HasOne(x => x.Servico)
                .WithMany(x => x.Funcionarios)
                .HasForeignKey(x => x.ServicoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AgendamentoFuncionario>()
                .HasKey(x => new
                {
                    x.AgendamentoId,
                    x.FuncionarioId
                });

            builder.Entity<AgendamentoFuncionario>()
                .HasOne(x => x.Agendamento)
                .WithMany(x => x.Funcionarios)
                .HasForeignKey(x => x.AgendamentoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AgendamentoFuncionario>()
                .HasOne(x => x.Funcionario)
                .WithMany(x => x.Agendamentos)
                .HasForeignKey(x => x.FuncionarioId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AgendamentoFuncionario>()
                .Property(x => x.PercentualComissao)
                .HasPrecision(5, 2);

            builder.Entity<AgendamentoFuncionario>()
                .Property(x => x.ValorComissao)
                .HasPrecision(10, 2);

            builder.Entity<Funcionario>()
    .HasIndex(x => x.EmpresaId);

            #endregion

            #region Plano

            builder.Entity<Plano>()
                .Property(x => x.ValorMensal)
                .HasPrecision(10, 2);

            builder.Entity<Plano>()
                .Property(x => x.ValorAnual)
                .HasPrecision(10, 2);

            #endregion

            #region Financeiro

            // ---- CategoriaFinanceira ----

            builder.Entity<CategoriaFinanceira>()
                .HasOne(x => x.Empresa)
                .WithMany(e => e.CategoriasFinanceiras)
                .HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CategoriaFinanceira>()
                .Property(x => x.Tipo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Entity<CategoriaFinanceira>()
                .HasIndex(x => x.EmpresaId);

            // ---- ContaReceber ----

            builder.Entity<ContaReceber>()
                .Property(x => x.ValorPrevisto)
                .HasPrecision(10, 2);

            builder.Entity<ContaReceber>()
                .Property(x => x.ValorRecebido)
                .HasPrecision(10, 2);

            builder.Entity<ContaReceber>()
                .Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Entity<ContaReceber>()
                .Property(x => x.FormaPagamento)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Entity<ContaReceber>()
                .HasOne(x => x.Empresa)
                .WithMany(e => e.ContasReceber)
                .HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ContaReceber>()
                .HasOne(x => x.Agendamento)
                .WithMany()
                .HasForeignKey(x => x.AgendamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ContaReceber>()
                .HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ContaReceber>()
                .HasOne(x => x.Categoria)
                .WithMany()
                .HasForeignKey(x => x.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ContaReceber>()
                .HasIndex(x => x.EmpresaId);

            builder.Entity<ContaReceber>()
                .HasIndex(x => new { x.EmpresaId, x.DataVencimento });

            // Trava no banco: um agendamento nunca gera duas contas a receber.
            builder.Entity<ContaReceber>()
                .HasIndex(x => x.AgendamentoId)
                .IsUnique()
                .HasFilter("[AgendamentoId] IS NOT NULL");

            // ---- ContaPagar ----

            builder.Entity<ContaPagar>()
                .Property(x => x.ValorPrevisto)
                .HasPrecision(10, 2);

            builder.Entity<ContaPagar>()
                .Property(x => x.ValorPago)
                .HasPrecision(10, 2);

            builder.Entity<ContaPagar>()
                .Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Entity<ContaPagar>()
                .Property(x => x.FormaPagamento)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Entity<ContaPagar>()
                .HasOne(x => x.Empresa)
                .WithMany(e => e.ContasPagar)
                .HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ContaPagar>()
                .HasOne(x => x.Funcionario)
                .WithMany()
                .HasForeignKey(x => x.FuncionarioId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ContaPagar>()
                .HasOne(x => x.Categoria)
                .WithMany()
                .HasForeignKey(x => x.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ContaPagar>()
                .HasIndex(x => x.EmpresaId);

            builder.Entity<ContaPagar>()
                .HasIndex(x => new { x.EmpresaId, x.DataVencimento });

            // ---- MovimentacaoFinanceira (Caixa) ----

            builder.Entity<MovimentacaoFinanceira>()
                .Property(x => x.Valor)
                .HasPrecision(10, 2);

            builder.Entity<MovimentacaoFinanceira>()
                .Property(x => x.Tipo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Entity<MovimentacaoFinanceira>()
                .Property(x => x.Origem)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Entity<MovimentacaoFinanceira>()
                .Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Entity<MovimentacaoFinanceira>()
                .Property(x => x.FormaPagamento)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Entity<MovimentacaoFinanceira>()
                .HasOne(x => x.Empresa)
                .WithMany(e => e.MovimentacoesFinanceiras)
                .HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MovimentacaoFinanceira>()
                .HasOne(x => x.ContaReceber)
                .WithMany(c => c.Movimentacoes)
                .HasForeignKey(x => x.ContaReceberId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<MovimentacaoFinanceira>()
                .HasOne(x => x.ContaPagar)
                .WithMany(c => c.Movimentacoes)
                .HasForeignKey(x => x.ContaPagarId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<MovimentacaoFinanceira>()
                .HasOne(x => x.Categoria)
                .WithMany()
                .HasForeignKey(x => x.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<MovimentacaoFinanceira>()
                .HasOne(x => x.Usuario)
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<MovimentacaoFinanceira>()
                .HasOne(x => x.MovimentacaoOrigemEstorno)
                .WithMany()
                .HasForeignKey(x => x.MovimentacaoOrigemEstornoId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<MovimentacaoFinanceira>()
                .HasIndex(x => x.EmpresaId);

            builder.Entity<MovimentacaoFinanceira>()
                .HasIndex(x => new { x.EmpresaId, x.DataMovimento });

            // ---- Comissão (AgendamentoFuncionario -> ContaPagar) ----

            builder.Entity<AgendamentoFuncionario>()
                .HasOne(x => x.ContaPagar)
                .WithMany(c => c.Comissoes)
                .HasForeignKey(x => x.ContaPagarId)
                .OnDelete(DeleteBehavior.SetNull);

            #endregion

            builder.Entity<Funcionario>()
    .HasIndex(x => x.EmpresaId);

            builder.Entity<Servico>()
                .HasIndex(x => x.EmpresaId);

            builder.Entity<Agendamento>()
                .HasIndex(x => new
                {
                    x.EmpresaId,
                    x.DataHora
                });

            builder.Entity<Funcionario>()
                .HasIndex(x => x.UserId)
                .IsUnique();

            builder.Entity<Cliente>()
                .HasIndex(x => x.UserId)
                .IsUnique();

            #region Galeria e Avaliações

            builder.Entity<EmpresaFoto>()
                .HasOne(f => f.Empresa)
                .WithMany(e => e.Fotos)
                .HasForeignKey(f => f.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Avaliacao>()
                .HasOne(a => a.Empresa)
                .WithMany(e => e.Avaliacoes)
                .HasForeignKey(a => a.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Avaliacao>()
                .HasOne(a => a.Cliente)
                .WithMany(c => c.Avaliacoes)
                .HasForeignKey(a => a.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Um cliente avalia cada empresa uma única vez — reenviar edita a
            // avaliação anterior em vez de duplicar.
            builder.Entity<Avaliacao>()
                .HasIndex(a => new { a.EmpresaId, a.ClienteId })
                .IsUnique();

            #endregion

            #region Planos de Serviço (controle de crédito, sem cobrança automática)

            builder.Entity<PlanoServico>()
                .HasOne(p => p.Empresa)
                .WithMany(e => e.PlanosServico)
                .HasForeignKey(p => p.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PlanoServico>()
                .Property(p => p.ValorReferencia)
                .HasPrecision(10, 2);

            builder.Entity<PlanoServico>()
                .Property(p => p.PercentualJurosAtraso)
                .HasPrecision(5, 2);

            builder.Entity<PlanoServicoItem>()
                .HasKey(x => new { x.PlanoServicoId, x.ServicoId });

            builder.Entity<PlanoServicoItem>()
                .HasOne(x => x.PlanoServico)
                .WithMany(p => p.Servicos)
                .HasForeignKey(x => x.PlanoServicoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PlanoServicoItem>()
                .HasOne(x => x.Servico)
                .WithMany()
                .HasForeignKey(x => x.ServicoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AssinaturaPlanoServico>()
                .HasOne(a => a.PlanoServico)
                .WithMany(p => p.Assinaturas)
                .HasForeignKey(a => a.PlanoServicoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AssinaturaPlanoServico>()
                .HasOne(a => a.Cliente)
                .WithMany()
                .HasForeignKey(a => a.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Agendamento>()
                .HasOne(a => a.AssinaturaPlanoServico)
                .WithMany()
                .HasForeignKey(a => a.AssinaturaPlanoServicoId)
                .OnDelete(DeleteBehavior.SetNull);

            #endregion
        }
    }
}