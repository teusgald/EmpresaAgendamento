using System.ComponentModel.DataAnnotations;
using EmpresaAgendamento.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class PlanoServicoViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome do plano.")]
        [StringLength(150)]
        public string Nome { get; set; } = null!;

        [Required(ErrorMessage = "Selecione a periodicidade.")]
        public PeriodicidadePlanoServico Periodicidade { get; set; }

        [Range(1, 999, ErrorMessage = "Informe quantas sessões o plano dá por período.")]
        public int QuantidadeUsos { get; set; }

        public decimal? ValorReferencia { get; set; }

        public decimal? PercentualJurosAtraso { get; set; }

        public bool Ativo { get; set; } = true;

        public List<int> ServicosSelecionados { get; set; } = new();

        public List<SelectListItem> ServicosDisponiveis { get; set; } = new();
    }
}
