using System.ComponentModel.DataAnnotations;
using EmpresaAgendamento.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class ProgramaFidelidadeViewModel
    {
        public int Id { get; set; }

        // Null = qualquer serviço.
        public int? ServicoId { get; set; }

        public bool Ativo { get; set; } = true;

        [Range(1, 100)]
        public int VisitasNecessarias { get; set; } = 5;

        public TipoDescontoFidelidade TipoDesconto { get; set; } = TipoDescontoFidelidade.Percentual;

        [Range(0.01, 100000)]
        public decimal ValorDesconto { get; set; } = 10;

        public List<SelectListItem> ServicosDisponiveis { get; set; } = new();
    }

    public class ProgramaFidelidadeListItemViewModel
    {
        public int Id { get; set; }
        public string? ServicoNome { get; set; }
        public bool Ativo { get; set; }
        public int VisitasNecessarias { get; set; }
        public TipoDescontoFidelidade TipoDesconto { get; set; }
        public decimal ValorDesconto { get; set; }
    }

    public class FidelidadeClienteItemViewModel
    {
        public int ProgramaFidelidadeId { get; set; }
        public string ProgramaNome { get; set; } = string.Empty;
        public int VisitasNecessarias { get; set; }
        public int ClienteId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int VisitasContadas { get; set; }
        public int DescontosDisponiveis { get; set; }
        public int DescontosUsados { get; set; }
    }

    public class FidelidadeIndexViewModel
    {
        public List<ProgramaFidelidadeListItemViewModel> Programas { get; set; } = new();
        public List<FidelidadeClienteItemViewModel> Clientes { get; set; } = new();
    }
}
