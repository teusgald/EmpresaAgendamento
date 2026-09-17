using System.ComponentModel.DataAnnotations;

namespace EmpresaAgendamento.Models.Enums
{
    public enum NivelAcessoFuncionario
    {
        // Só Calendário, Agendamentos, Clientes e Atendimento.
        [Display(Name = "Padrão")]
        Padrao = 1,

        // Também vê Serviços, Fidelidade e o módulo Financeiro. Nunca vê
        // Funcionários, Assinatura/Empresas — isso continua exclusivo do
        // dono da empresa.
        [Display(Name = "Gerente")]
        Gerente = 2
    }
}
