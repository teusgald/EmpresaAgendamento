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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // =========================
            // 🔐 Cliente ↔ Identity (1:1)
            // =========================
            builder.Entity<Cliente>()
                .HasOne(c => c.User)
                .WithOne(u => u.Cliente)
                .HasForeignKey<Cliente>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // 🏢 Empresa -> Serviços
            // =========================
            builder.Entity<Servico>()
                .HasOne(s => s.Empresa)
                .WithMany(e => e.Servicos)
                .HasForeignKey(s => s.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================
            // 🏢 Empresa -> Agendamentos
            // =========================
            builder.Entity<Agendamento>()
                .HasOne(a => a.Empresa)
                .WithMany(e => e.Agendamentos)
                .HasForeignKey(a => a.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================
            // 👤 Cliente -> Agendamentos
            // =========================
            builder.Entity<Agendamento>()
                .HasOne(a => a.Cliente)
                .WithMany(c => c.Agendamentos)
                .HasForeignKey(a => a.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================
            // 🛠 Serviço -> Agendamentos
            // =========================
            builder.Entity<Agendamento>()
                .HasOne(a => a.Servico)
                .WithMany(s => s.Agendamentos)
                .HasForeignKey(a => a.ServicoId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================
            // 🔗 EmpresaCliente (Many-to-Many)
            // =========================
            builder.Entity<EmpresaCliente>()
                .HasKey(ec => new { ec.EmpresaId, ec.ClienteId });

            builder.Entity<EmpresaCliente>()
                .HasOne(ec => ec.Empresa)
                .WithMany(e => e.EmpresaClientes)
                .HasForeignKey(ec => ec.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EmpresaCliente>()
                .HasOne(ec => ec.Cliente)
                .WithMany(c => c.EmpresaClientes)
                .HasForeignKey(ec => ec.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // 👨‍💼 User ↔ Empresa
            // =========================
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Empresa)
                .WithMany(e => e.Usuarios)
                .HasForeignKey(u => u.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================
            // 💰 CONFIGURAÇÃO DE DECIMAL (Preco)
            // =========================
            builder.Entity<Servico>()
                .Property(s => s.Preco)
                .HasPrecision(10, 2);

            // =========================
            // 🧾 CONFIGURAÇÕES DE CAMPOS
            // =========================
            builder.Entity<Empresa>()
                .Property(e => e.Nome)
                .HasMaxLength(150)
                .IsRequired();

            builder.Entity<Empresa>()
                .Property(e => e.EmailContato)
                .HasMaxLength(150);

            builder.Entity<Cliente>()
                .Property(c => c.Nome)
                .HasMaxLength(150)
                .IsRequired();

            builder.Entity<Servico>()
                .Property(s => s.Nome)
                .HasMaxLength(150)
                .IsRequired();

            // =========================
            // 📅 DATA E SOFT DELETE
            // =========================
            builder.Entity<Agendamento>()
                .Property(a => a.DataCriacao)
                .HasDefaultValueSql("GETDATE()");

            builder.Entity<Agendamento>()
                .Property(a => a.Ativo)
                .HasDefaultValue(true);

            // =========================
            // 🔁 ENUM StatusAgendamento → STRING NO BANCO
            // =========================
            builder.Entity<Agendamento>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
        }
    }
}