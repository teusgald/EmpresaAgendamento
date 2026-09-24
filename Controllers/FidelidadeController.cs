using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("Fidelidade")]
    [Authorize(Roles = "Empresa,Funcionario")]
    public class FidelidadeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FidelidadeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        private async Task<List<SelectListItem>> CarregarServicosDisponiveisAsync(int empresaId)
        {
            return await _context.Servicos
                .Where(s => s.EmpresaId == empresaId && s.Ativo)
                .OrderBy(s => s.Nome)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Nome })
                .ToListAsync();
        }

        [HttpGet("")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Visualizar" })]
        public async Task<IActionResult> Index()
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var programas = await _context.ProgramasFidelidade
                    .Include(p => p.Servico)
                    .Where(p => p.EmpresaId == empresaId)
                    .OrderBy(p => p.Servico == null ? 0 : 1)
                    .ThenBy(p => p.Servico!.Nome)
                    .Select(p => new ProgramaFidelidadeListItemViewModel
                    {
                        Id = p.Id,
                        ServicoNome = p.Servico != null ? p.Servico.Nome : null,
                        Ativo = p.Ativo,
                        VisitasNecessarias = p.VisitasNecessarias,
                        TipoDesconto = p.TipoDesconto,
                        ValorDesconto = p.ValorDesconto
                    })
                    .ToListAsync();

                var clientes = await _context.FidelidadeClientes
                    .Include(f => f.Programa).ThenInclude(p => p!.Servico)
                    .Include(f => f.Cliente)
                    .Where(f => f.Programa!.EmpresaId == empresaId
                        && (f.VisitasContadas > 0 || f.DescontosDisponiveis > 0 || f.DescontosUsados > 0))
                    .OrderByDescending(f => f.DescontosDisponiveis)
                    .ThenByDescending(f => f.VisitasContadas)
                    .Select(f => new FidelidadeClienteItemViewModel
                    {
                        ProgramaFidelidadeId = f.ProgramaFidelidadeId,
                        ProgramaNome = f.Programa!.Servico != null ? f.Programa.Servico.Nome : "Geral (qualquer serviço)",
                        VisitasNecessarias = f.Programa.VisitasNecessarias,
                        ClienteId = f.ClienteId,
                        Nome = f.Cliente.Nome,
                        VisitasContadas = f.VisitasContadas,
                        DescontosDisponiveis = f.DescontosDisponiveis,
                        DescontosUsados = f.DescontosUsados
                    })
                    .ToListAsync();

                var vm = new FidelidadeIndexViewModel
                {
                    Programas = programas,
                    Clientes = clientes
                };

                return View(vm);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar fidelidade.");
                return View(new FidelidadeIndexViewModel());
            }
        }

        [HttpGet("Create")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Criar" })]
        public async Task<IActionResult> Create()
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var vm = new ProgramaFidelidadeViewModel
                {
                    ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value)
                };

                return View(vm);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao abrir formulário.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Criar" })]
        public async Task<IActionResult> Create(ProgramaFidelidadeViewModel vm)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                if (vm.ServicoId.HasValue &&
                    !await _context.Servicos.AnyAsync(s => s.Id == vm.ServicoId && s.EmpresaId == empresaId))
                {
                    ModelState.AddModelError("", "Serviço não encontrado.");
                }

                var jaExiste = await _context.ProgramasFidelidade
                    .AnyAsync(p => p.EmpresaId == empresaId && p.ServicoId == vm.ServicoId);

                if (jaExiste)
                {
                    ModelState.AddModelError("", vm.ServicoId == null
                        ? "Já existe um programa geral (qualquer serviço) cadastrado."
                        : "Já existe um programa de fidelidade cadastrado pra esse serviço.");
                }

                if (!ModelState.IsValid)
                {
                    vm.ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value);
                    return View(vm);
                }

                var programa = new ProgramaFidelidade
                {
                    EmpresaId = empresaId.Value,
                    ServicoId = vm.ServicoId,
                    Ativo = vm.Ativo,
                    VisitasNecessarias = vm.VisitasNecessarias,
                    TipoDesconto = vm.TipoDesconto,
                    ValorDesconto = vm.ValorDesconto
                };

                _context.ProgramasFidelidade.Add(programa);
                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Programa de fidelidade criado.");
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao criar programa de fidelidade.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet("Edit/{id}")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Editar" })]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var programa = await _context.ProgramasFidelidade
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (programa == null)
                {
                    ToastHelper.Error(TempData, "Programa de fidelidade não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                var vm = new ProgramaFidelidadeViewModel
                {
                    Id = programa.Id,
                    ServicoId = programa.ServicoId,
                    Ativo = programa.Ativo,
                    VisitasNecessarias = programa.VisitasNecessarias,
                    TipoDesconto = programa.TipoDesconto,
                    ValorDesconto = programa.ValorDesconto,
                    ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value)
                };

                return View(vm);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar programa de fidelidade.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Editar" })]
        public async Task<IActionResult> Edit(int id, ProgramaFidelidadeViewModel vm)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var programa = await _context.ProgramasFidelidade
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (programa == null)
                {
                    ToastHelper.Error(TempData, "Programa de fidelidade não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                if (vm.ServicoId.HasValue &&
                    !await _context.Servicos.AnyAsync(s => s.Id == vm.ServicoId && s.EmpresaId == empresaId))
                {
                    ModelState.AddModelError("", "Serviço não encontrado.");
                }

                var jaExiste = await _context.ProgramasFidelidade
                    .AnyAsync(p => p.Id != id && p.EmpresaId == empresaId && p.ServicoId == vm.ServicoId);

                if (jaExiste)
                {
                    ModelState.AddModelError("", vm.ServicoId == null
                        ? "Já existe um programa geral (qualquer serviço) cadastrado."
                        : "Já existe um programa de fidelidade cadastrado pra esse serviço.");
                }

                if (!ModelState.IsValid)
                {
                    vm.ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value);
                    return View(vm);
                }

                programa.ServicoId = vm.ServicoId;
                programa.Ativo = vm.Ativo;
                programa.VisitasNecessarias = vm.VisitasNecessarias;
                programa.TipoDesconto = vm.TipoDesconto;
                programa.ValorDesconto = vm.ValorDesconto;

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Programa de fidelidade atualizado.");
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao atualizar programa de fidelidade.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Excluir" })]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var programa = await _context.ProgramasFidelidade
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (programa != null)
                {
                    _context.ProgramasFidelidade.Remove(programa);
                    await _context.SaveChangesAsync();
                    ToastHelper.Success(TempData, "Programa de fidelidade excluído.");
                }

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao excluir programa de fidelidade.");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("MarcarDescontoUsado")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "Fidelidade", "Editar" })]
        public async Task<IActionResult> MarcarDescontoUsado(int programaFidelidadeId, int clienteId)
        {
            try
            {
                var empresaId = await GetEmpresaId();
                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "Account");
                }

                var progresso = await _context.FidelidadeClientes
                    .Include(f => f.Programa)
                    .FirstOrDefaultAsync(f => f.ProgramaFidelidadeId == programaFidelidadeId
                        && f.ClienteId == clienteId
                        && f.Programa!.EmpresaId == empresaId);

                if (progresso == null || progresso.DescontosDisponiveis <= 0)
                {
                    ToastHelper.Warning(TempData, "Esse cliente não tem desconto de fidelidade disponível.");
                    return RedirectToAction(nameof(Index));
                }

                progresso.DescontosDisponiveis--;
                progresso.DescontosUsados++;
                progresso.DataUltimaAtualizacao = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Desconto de fidelidade marcado como usado.");
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao marcar desconto como usado.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
