using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;

namespace EmpresaAgendamento.Services
{
    public interface IEmpresaService
    {
        // Não faz sign-in automático — o e-mail precisa ser confirmado antes
        // do primeiro login. Retorna o usuário criado para o controller gerar
        // o token de confirmação e enviar o e-mail. tipoPlanoEscolhido vem da
        // página de vendas (mensal/anual) e já fica salvo na Empresa.
        Task<(bool Success, string Error, ApplicationUser? User)> RegisterAsync(
            EmpresaRegisterViewModel model, string? tipoPlanoEscolhido = null);

        Task<(bool Success, string Error)> LoginAsync(EmpresaLoginViewModel model);
    }
}
