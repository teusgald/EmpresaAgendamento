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

            builder.Entity<Empresa>()
                .HasIndex(e => e.DocumentoNumero)
                .IsUnique();

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
        }
    }
}