using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    // Perfil de acesso configurável pela própria empresa — substitui o
    // NivelAcessoFuncionario fixo (Padrão/Gerente) por algo que o dono pode
    // criar/editar, com permissão por módulo em vez de tudo-ou-nada.
    public class Perfil
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; } = null!;

        [Required]
        [StringLength(60)]
        public string Nome { get; set; } = null!;

        public bool Ativo { get; set; } = true;

        public ICollection<PerfilPermissao> Permissoes { get; set; } = new List<PerfilPermissao>();

        public ICollection<Funcionario> Funcionarios { get; set; } = new List<Funcionario>();
    }
}
