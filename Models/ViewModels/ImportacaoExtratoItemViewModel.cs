using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmpresaAgendamento.Models.ViewModels
{
    public class ImportacaoExtratoItemViewModel
    {
        public DateTime Data { get; set; }

        // Positivo = entrada, negativo = saída (mesma convenção do OFX).
        public decimal Valor { get; set; }

        public string Descricao { get; set; } = "";

        public string? FitId { get; set; }

        public bool JaImportado { get; set; }

        // "conciliar" (baixa uma conta existente), "novo" (lançamento avulso
        // no caixa) ou "ignorar" (não importa essa linha).
        public string Acao { get; set; } = "novo";

        public int? ContaSelecionadaId { get; set; }

        public int? CategoriaId { get; set; }

        // Só usado pra montar o <select> na tela de revisão — não é reenviado no POST.
        public List<SelectListItem> ContasDisponiveis { get; set; } = new();
    }
}
