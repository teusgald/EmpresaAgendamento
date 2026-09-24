using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    // CRUD de usuários do sistema pro dono da plataforma. Busca exige pelo
    // menos um filtro (termo ou tipo) — nunca lista todo mundo de uma vez,
    // pra não virar uma query gigante em produção.
    [Authorize(Roles = "SuperAdmin")]
    [Route("superadmin/usuarios")]
    public class SuperAdminUsuariosController : Controller
    {
        private const int LimiteResultados = 50;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuperAdminUsuariosController> _logger;

        public SuperAdminUsuariosController(ApplicationDbContext context, ILogger<SuperAdminUsuariosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? termo, string? tipo)
        {
            var model = new SuperAdminUsuariosBuscaViewModel
            {
                Termo = termo,
                Tipo = tipo
            };

            var temFiltro = !string.IsNullOrWhiteSpace(termo) || !string.IsNullOrWhiteSpace(tipo);

            if (!temFiltro)
            {
                return View(model);
            }

            model.Buscou = true;

            try
            {
                var query =
                    from u in _context.Users
                    join ur in _context.UserRoles on u.Id equals ur.UserId
                    join r in _context.Roles on ur.RoleId equals r.Id
                    where r.Name == "Empresa" || r.Name == "Funcionario" || r.Name == "Cliente"
                    select new { u, RoleName = r.Name };

                if (!string.IsNullOrWhiteSpace(tipo))
                {
                    query = query.Where(x => x.RoleName == tipo);
                }

                if (!string.IsNullOrWhiteSpace(termo))
                {
                    var termoBusca = termo.Trim();
                    query = query.Where(x =>
                        (x.u.Email != null && x.u.Email.Contains(termoBusca)) ||
                        (x.u.NomeCompleto != null && x.u.NomeCompleto.Contains(termoBusca)));
                }

                var resultados = await query
                    .OrderBy(x => x.u.NomeCompleto)
                    .Take(LimiteResultados)
                    .Select(x => new SuperAdminUsuarioResultViewModel
                    {
                        UserId = x.u.Id,
                        Nome = x.u.NomeCompleto,
                        Email = x.u.Email,
                        Tipo = x.RoleName!,
                        Ativo = x.u.Ativo,
                        EmpresaId = x.u.EmpresaId,
                        EmpresaNome = x.u.Empresa != null ? x.u.Empresa.Nome : null,
                        PlanoNome = x.u.Empresa != null && x.u.Empresa.Plano != null ? x.u.Empresa.Plano.Nome : null,
                        EmpresaVip = x.u.Empresa != null && x.u.Empresa.VipAcesso
                    })
                    .ToListAsync();

                model.Resultados = resultados;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar usuários (termo {Termo}, tipo {Tipo}).", termo, tipo);
                ToastHelper.Error(TempData, "Erro ao buscar usuários.");
                model.Resultados = new List<SuperAdminUsuarioResultViewModel>();
                return View(model);
            }
        }

        [HttpGet("empresa/{empresaId:int}/editar")]
        public async Task<IActionResult> EditarEmpresa(int empresaId)
        {
            try
            {
                var empresa = await _context.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);

                if (empresa == null)
                {
                    return NotFound();
                }

                var planos = await _context.Planos
                    .Where(p => p.Ativo)
                    .OrderBy(p => p.ValorMensal)
                    .ToListAsync();

                var model = new SuperAdminEditarEmpresaViewModel
                {
                    EmpresaId = empresa.Id,
                    EmpresaNome = empresa.Nome,
                    PlanoId = empresa.PlanoId,
                    VipAcesso = empresa.VipAcesso,
                    WhatsAppPhoneNumberId = empresa.WhatsAppPhoneNumberId,
                    SimpliAiBetaAtivo = empresa.SimpliAiBetaAtivo,
                    PlanosDisponiveis = planos
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao abrir edição da empresa {EmpresaId}.", empresaId);
                ToastHelper.Error(TempData, "Erro ao abrir edição da empresa.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("empresa/{empresaId:int}/editar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarEmpresa(int empresaId, SuperAdminEditarEmpresaViewModel model)
        {
            try
            {
                var empresa = await _context.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);

                if (empresa == null)
                {
                    return NotFound();
                }

                empresa.PlanoId = model.PlanoId;
                empresa.VipAcesso = model.VipAcesso;
                empresa.WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(model.WhatsAppPhoneNumberId)
                    ? null
                    : model.WhatsAppPhoneNumberId.Trim();
                empresa.SimpliAiBetaAtivo = model.SimpliAiBetaAtivo;

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, $"Empresa \"{empresa.Nome}\" atualizada com sucesso.");

                return RedirectToAction(nameof(Index), new { termo = empresa.Nome });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar empresa {EmpresaId}.", empresaId);
                ToastHelper.Error(TempData, "Erro ao atualizar empresa.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
