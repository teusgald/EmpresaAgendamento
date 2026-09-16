using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class FuncionarioLoginViewModel
    {
        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Informe sua senha.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;
    }
}
