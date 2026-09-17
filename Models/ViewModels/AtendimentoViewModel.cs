using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class AtendimentoViewModel
    {
        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = "";

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = "";

        // "Empresa", "Cliente" ou "Funcionário" — só pra dar contexto no e-mail.
        public string Origem { get; set; } = "";

        [Required(ErrorMessage = "Informe o assunto.")]
        [StringLength(150)]
        public string Assunto { get; set; } = "";

        [Required(ErrorMessage = "Descreva o que você precisa.")]
        [StringLength(3000)]
        public string Mensagem { get; set; } = "";
    }
}
