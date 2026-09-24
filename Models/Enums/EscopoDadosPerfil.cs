namespace EmpresaAgendamento.Models.Enums
{
    // Quando um funcionário com esse perfil abre um módulo com escopo por
    // funcionário (hoje: Comissões) — Proprios = só o que é dele,
    // Todos = vê da empresa inteira. Módulo sem esse vínculo natural
    // (ex.: Clientes, Financeiro) ignora esse campo, sempre trata como Todos.
    public enum EscopoDadosPerfil
    {
        Proprios = 1,
        Todos = 2
    }
}
