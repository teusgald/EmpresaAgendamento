using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.ViewModels
{
    public class FuncionarioViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        public string? Telefone { get; set; }

        public string? Cargo { get; set; }

        public string? Observacoes { get; set; }

        public string? FotoUrl { get; set; }

        public decimal? PercentualComissaoPadrao { get; set; }

        public decimal? ValorComissaoFixa { get; set; }

        public bool Ativo { get; set; }

        public List<int> ServicosSelecionados { get; set; }
            = new();

        public List<SelectListItem> ServicosDisponiveis { get; set; }
            = new();
    }
}