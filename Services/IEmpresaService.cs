using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;

namespace EmpresaAgendamento.Services
{
    public interface IEmpresaService
    {
        // Não faz sign-in automático — o e-mail precisa ser confirmado antes
        // do primeiro login. Retorna o usuário criado para o controller gerar
        // o token de confirmação e enviar o e-mail. tipoPlanoEscolhido
        // (mensal/semestral/anual) e nomePlanoEscolhido (Start/Pro/Business)
        // vêm da página de vendas e já ficam salvos na Empresa.
        Task<(bool Success, string Error, ApplicationUser? User)> RegisterAsync(
            EmpresaRegisterViewModel model, string? tipoPlanoEscolhido = null, string? nomePlanoEscolhido = null);

        Task<(bool Success, string Error)> LoginAsync(EmpresaLoginViewModel model);
    }
}
