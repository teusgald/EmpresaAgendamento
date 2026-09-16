using System.ComponentModel.DataAnnotations;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models
{
    // Plano recorrente que a EMPRESA vende pro CLIENTE final (ex.: "Personal
    // Mensal — 12 sessões/mês"). Não tem nenhuma relação com o Plano do SaaS
    // (aquele é o que a empresa paga pra usar o Simpli Time). Aqui não tem
    // cobrança automática — é só controle de crédito/uso; o pagamento em si
    // a empresa continua fazendo por fora (Pix, dinheiro etc.), como já fazia.
    public class PlanoServico
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = null!;

        [Required]
        public PeriodicidadePlanoServico Periodicidade { get; set; }

        [Range(1, 999)]
        public int QuantidadeUsos { get; set; }

        public decimal? ValorReferencia { get; set; }

        // % aplicado sobre o valor se o cliente atrasar o pagamento — só
        // informativo (mostrado no plano/e-mail), não tem cobrança automática.
        public decimal? PercentualJurosAtraso { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public ICollection<PlanoServicoItem> Servicos { get; set; }
            = new List<PlanoServicoItem>();

        public ICollection<AssinaturaPlanoServico> Assinaturas { get; set; }
            = new List<AssinaturaPlanoServico>();
    }
}
