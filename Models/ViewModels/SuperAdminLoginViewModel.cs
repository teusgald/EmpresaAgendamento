using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class SuperAdminLoginViewModel
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;
    }
}
