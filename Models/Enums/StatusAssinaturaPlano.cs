namespace EmpresaAgendamento.Models.Enums
{
    // Pendente = cliente solicitou (pelo público ou pelo portal dele) e
    // está esperando a empresa confirmar o pagamento/acordo por fora antes
    // de liberar os créditos.
    public enum StatusAssinaturaPlano
    {
        Pendente = 1,
        Ativa = 2,
        Cancelada = 3
    }
}
