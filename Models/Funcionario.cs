using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    public class Funcionario
    {
        public int Id { get; set; }

        // Mantido como ponte enquanto o Perfil granular (abaixo) não estiver
        // validado em produção — RequerGerenteFilter ainda lê esse campo.
        public NivelAcessoFuncionario NivelAcesso { get; set; } = NivelAcessoFuncionario.Padrao;

        public int? PerfilId { get; set; }
        public Perfil? Perfil { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = null!;

        [EmailAddress]
        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(25)]
        public string? Telefone { get; set; }

        [StringLength(100)]
        public string? Cargo { get; set; }
        [StringLength(500)]
        public string? Observacoes { get; set; }

        public string? FotoUrl { get; set; }

        public bool Ativo { get; set; } = true;

        public decimal? PercentualComissaoPadrao { get; set; }

        public decimal? ValorComissaoFixa { get; set; }

        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
        public DateTime? DataDemissao { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;
        #region Usuário

        public string? UserId { get; set; }

        public ApplicationUser? User { get; set; }

        #endregion

        public ICollection<FuncionarioHorario> Horarios { get; set; }
     = new List<FuncionarioHorario>();

        public ICollection<FuncionarioServico> Servicos { get; set; }
            = new List<FuncionarioServico>();

        public ICollection<AgendamentoFuncionario> Agendamentos { get; set; }
            = new List<AgendamentoFuncionario>();
    }
}