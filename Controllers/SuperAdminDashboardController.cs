using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    // Painel do dono do sistema — enxerga todos os dados de todas as
    // empresas. Só agrega contagens aqui (nada de listar linha a linha sem
    // filtro; isso fica na tela de Usuários, que exige filtro).
    [Authorize(Roles = "SuperAdmin")]
    [Route("superadmin")]
    public class SuperAdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuperAdminDashboardController> _logger;

        public SuperAdminDashboardController(ApplicationDbContext context, ILogger<SuperAdminDashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var totalEmpresas = await _context.Empresas.CountAsync();

                var empresasVip = await _context.Empresas.CountAsync(e => e.VipAcesso);

                var empresasPagantes = await _context.Empresas.CountAsync(e =>
                    !e.VipAcesso &&
                    (e.AssinaturaStatus == "active" || e.AssinaturaStatus == "trialing"));

                var empresasNaoPagantes = totalEmpresas - empresasVip - empresasPagantes;

                var totalFuncionarios = await _context.Funcionarios.CountAsync();
                var totalClientes = await _context.Clientes.CountAsync();

                var totalUsuarios = await _context.Users.CountAsync();
                var usuariosAtivos = await _context.Users.CountAsync(u => u.Ativo);

                var model = new SuperAdminDashboardViewModel
                {
                    TotalEmpresas = totalEmpresas,
                    EmpresasPagantes = empresasPagantes,
                    EmpresasVip = empresasVip,
                    EmpresasNaoPagantes = empresasNaoPagantes,
                    TotalFuncionarios = totalFuncionarios,
                    TotalClientes = totalClientes,
                    TotalUsuarios = totalUsuarios,
                    UsuariosAtivos = usuariosAtivos
                };

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar dashboard do SuperAdmin.");
                ToastHelper.Error(TempData, "Erro ao carregar o dashboard.");
                return View("Index", new SuperAdminDashboardViewModel());
            }
        }
    }
}
