using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    public class Plano
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; } = null!;

        public decimal ValorMensal { get; set; }

        public int LimiteFuncionarios { get; set; }

        public int LimiteAgendamentosMes { get; set; }

        public bool PermiteFinanceiro { get; set; }

        public bool PermiteWhatsapp { get; set; }

        public bool PermiteRelatorios { get; set; }

        public bool Ativo { get; set; } = true;

        public bool PermiteMultiFuncionarios { get; set; }

        public bool PermiteLandingPage { get; set; }

        public bool PermiteAPI { get; set; }

        // Simpli AI — assistente de chat com acesso a dado real da empresa.
        // 0 em LimiteIAMes = ilimitado, mesma convenção de LimiteAgendamentosMes.
        public bool PermiteIA { get; set; }

        public int LimiteIAMes { get; set; }

        // ─────────────────────────────────────────────
        // 💳 STRIPE (mapeamento do plano local para o Stripe)
        // ─────────────────────────────────────────────

        public decimal? ValorSemestral { get; set; }

        public decimal? ValorAnual { get; set; }

        [StringLength(60)]
        public string? StripePriceIdMensal { get; set; }

        [StringLength(60)]
        public string? StripePriceIdSemestral { get; set; }

        [StringLength(60)]
        public string? StripePriceIdAnual { get; set; }

        public ICollection<Empresa> Empresas { get; set; }
    = new List<Empresa>();
    }
}