namespace EmpresaAgendamento.Models.Enums
{
    public enum StatusAgendamento
    {
        Agendado = 1,
        Confirmado = 2,
        Cancelado = 3,
        Finalizado = 4,

        // Atendimento em curso — abre a comanda pra adicionar produtos
        // consumidos além do serviço (ex.: bebida, creme) antes de finalizar.
        EmAndamento = 5
    }
}