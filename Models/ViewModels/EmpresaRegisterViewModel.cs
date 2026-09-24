using System.ComponentModel.DataAnnotations;
using EmpresaAgendamento.Models.Enums;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class EmpresaRegisterViewModel
    {
        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(150)]
        public string NomeResponsavel { get; set; } = null!;

        [Required(ErrorMessage = "Informe o nome da empresa.")]
        [StringLength(150)]
        public string NomeEmpresa { get; set; } = null!;

        [Required(ErrorMessage = "Selecione o segmento do seu negócio.")]
        public CategoriaEmpresa? Categoria { get; set; }

        [Required(ErrorMessage = "Informe seu telefone/WhatsApp.")]
        [StringLength(25)]
        [Phone(ErrorMessage = "Telefone inválido.")]
        public string Telefone { get; set; } = null!;

        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Crie uma senha.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Confirme sua senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "As senhas não coincidem.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
