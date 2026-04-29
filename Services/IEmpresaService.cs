using EmpresaAgendamento.Models;

namespace EmpresaAgendamento.Services
{
    public interface IEmpresaService
    {
        Task<(bool Success, string Error)> RegisterAsync(EmpresaRegisterViewModel model);
        Task<(bool Success, string Error)> LoginAsync(EmpresaLoginViewModel model);
    }
}
