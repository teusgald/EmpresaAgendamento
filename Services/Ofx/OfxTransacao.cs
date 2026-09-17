namespace EmpresaAgendamento.Services.Ofx
{
    // Uma linha <STMTTRN> do extrato: Valor positivo = entrada (crédito),
    // negativo = saída (débito) — é assim que o próprio OFX representa.
    public record OfxTransacao(DateTime Data, decimal Valor, string Descricao, string? FitId);
}
