using EmpresaAgendamento.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class PerfilFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(60)]
        public string Nome { get; set; } = "";

        public List<PerfilPermissaoItemViewModel> Permissoes { get; set; } = new();
    }

    public class PerfilPermissaoItemViewModel
    {
        public string Modulo { get; set; } = "";
        public string ModuloLabel { get; set; } = "";
        public bool PodeVisualizar { get; set; }
        public bool PodeCriar { get; set; }
        public bool PodeEditar { get; set; }
        public bool PodeExcluir { get; set; }
        public EscopoDadosPerfil EscopoDados { get; set; } = EscopoDadosPerfil.Todos;
    }
}
