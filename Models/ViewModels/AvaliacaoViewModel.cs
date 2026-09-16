using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class AvaliacaoViewModel
    {
        [Required]
        public int EmpresaId { get; set; }

        [Range(1, 5, ErrorMessage = "Escolha de 1 a 5 estrelas.")]
        public int Nota { get; set; }

        [StringLength(500)]
        public string? Comentario { get; set; }
    }
}
