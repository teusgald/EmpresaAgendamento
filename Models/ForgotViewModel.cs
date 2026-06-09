using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models
{
    public class ForgotViewModel
    {
        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string Email { get; set; }
    }
}
