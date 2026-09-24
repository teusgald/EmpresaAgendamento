using EmpresaAgendamento.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    // Uma linha por módulo dentro de um Perfil. Módulo sem linha aqui =
    // sem acesso nenhum (RequerPermissaoFilter trata ausência como negado).
    public class PerfilPermissao
    {
        public int Id { get; set; }

        public int PerfilId { get; set; }
        public Perfil Perfil { get; set; } = null!;

        // "Clientes", "Servicos", "Produtos", "Financeiro", "Fidelidade",
        // "ContasReceber", "ContasPagar", "Comissoes", "CategoriasFinanceiras".
        [Required]
        [StringLength(40)]
        public string Modulo { get; set; } = null!;

        public bool PodeVisualizar { get; set; }
        public bool PodeCriar { get; set; }
        public bool PodeEditar { get; set; }
        public bool PodeExcluir { get; set; }

        public EscopoDadosPerfil EscopoDados { get; set; } = EscopoDadosPerfil.Todos;
    }
}
