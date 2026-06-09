using EmpresaAgendamento.Enums;
using Microsoft.AspNetCore.Identity;

namespace EmpresaAgendamento.Models
{
    public class ApplicationUser : IdentityUser
    {
        #region Dados

        public string? NomeCompleto { get; set; }

        #endregion

        #region Empresa

        public int? EmpresaId { get; set; }

        public Empresa? Empresa { get; set; }

        #endregion

        #region Cliente

        public Cliente? Cliente { get; set; }

        #endregion

        #region Funcionário

        public Funcionario? Funcionario { get; set; }

        #endregion

        #region Permissões

        public PerfilUsuario PerfilUsuario { get; set; }
            = PerfilUsuario.Funcionario;

        #endregion

        #region Controle

        public bool Ativo { get; set; } = true;

        public DateTime DataCadastro { get; set; }
            = DateTime.UtcNow;

        #endregion
    }
}