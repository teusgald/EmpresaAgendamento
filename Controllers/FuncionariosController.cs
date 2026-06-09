using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Empresa")]
public class FuncionariosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FuncionariosController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<int?> GetEmpresaId()
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }
        catch
        {
            return null;
        }
    }

    // =========================
    // INDEX
    // =========================
    [HttpGet("/Funcionarios")]
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            int pageSize = 10;

            var query = _context.Funcionarios
                .Where(f => f.EmpresaId == empresaId)
                .Include(f => f.Servicos)
                    .ThenInclude(fs => fs.Servico)
                .OrderBy(f => f.Nome);

            var totalItems = await query.CountAsync();

            var funcionarios = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(funcionarios);
        }
        catch (Exception ex)
        {
            ToastHelper.Error(TempData, "Erro ao carregar funcionários.");
            return RedirectToAction("Index", "Home");
        }
    }

    // =========================
    // CREATE GET
    // =========================
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

            var vm = new FuncionarioViewModel();

            vm.ServicosDisponiveis = await _context.Servicos
                .Where(s => s.EmpresaId == empresaId && s.Ativo)
                .OrderBy(s => s.Nome)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Nome
                })
                .ToListAsync();

            return View(vm);
        }
        catch
        {
            ToastHelper.Error(TempData, "Erro ao abrir formulário.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // CREATE POST
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FuncionarioViewModel vm)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                vm.ServicosDisponiveis = await _context.Servicos
                    .Where(s => s.EmpresaId == empresaId && s.Ativo)
                    .OrderBy(s => s.Nome)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Nome
                    })
                    .ToListAsync();

                ToastHelper.Warning(TempData, "Verifique os dados informados.");
                return View(vm);
            }

            var funcionario = new Funcionario
            {
                Nome = vm.Nome,
                Email = vm.Email,
                Telefone = vm.Telefone,
                Cargo = vm.Cargo,
                Observacoes = vm.Observacoes,
                FotoUrl = vm.FotoUrl,
                PercentualComissaoPadrao = vm.PercentualComissaoPadrao,
                ValorComissaoFixa = vm.ValorComissaoFixa,
                EmpresaId = empresaId.Value,
                Ativo = true
            };

            _context.Funcionarios.Add(funcionario);
            await _context.SaveChangesAsync();

            foreach (var servicoId in vm.ServicosSelecionados ?? new List<int>())
            {
                _context.FuncionariosServicos.Add(new FuncionarioServico
                {
                    FuncionarioId = funcionario.Id,
                    ServicoId = servicoId
                });
            }

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Funcionário cadastrado com sucesso!");
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            ToastHelper.Error(TempData, "Erro ao criar funcionário.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // EDIT GET
    // =========================
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            var funcionario = await _context.Funcionarios
                .Include(f => f.Servicos)
                .FirstOrDefaultAsync(f =>
                    f.Id == id &&
                    f.EmpresaId == empresaId);

            if (funcionario == null)
            {
                ToastHelper.Error(TempData, "Funcionário não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            var vm = new FuncionarioViewModel
            {
                Id = funcionario.Id,
                Nome = funcionario.Nome,
                Email = funcionario.Email,
                Telefone = funcionario.Telefone,
                Cargo = funcionario.Cargo,
                Observacoes = funcionario.Observacoes,
                FotoUrl = funcionario.FotoUrl,
                PercentualComissaoPadrao = funcionario.PercentualComissaoPadrao,
                ValorComissaoFixa = funcionario.ValorComissaoFixa,
                ServicosSelecionados = funcionario.Servicos.Select(x => x.ServicoId).ToList()
            };

            vm.ServicosDisponiveis = await _context.Servicos
                .Where(s => s.EmpresaId == empresaId)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Nome
                })
                .ToListAsync();

            return View(vm);
        }
        catch
        {
            ToastHelper.Error(TempData, "Erro ao carregar funcionário.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // EDIT POST
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FuncionarioViewModel vm)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            if (id != vm.Id)
            {
                ToastHelper.Error(TempData, "Funcionário inválido.");
                return RedirectToAction(nameof(Index));
            }

            var funcionario = await _context.Funcionarios
                .Include(f => f.Servicos)
                .FirstOrDefaultAsync(f =>
                    f.Id == id &&
                    f.EmpresaId == empresaId);

            if (funcionario == null)
            {
                ToastHelper.Error(TempData, "Funcionário não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                vm.ServicosDisponiveis = await _context.Servicos
                    .Where(s => s.EmpresaId == empresaId && s.Ativo)
                    .OrderBy(s => s.Nome)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Nome
                    })
                    .ToListAsync();

                return View(vm);
            }

            funcionario.Nome = vm.Nome;
            funcionario.Email = vm.Email;
            funcionario.Telefone = vm.Telefone;
            funcionario.Cargo = vm.Cargo;
            funcionario.Observacoes = vm.Observacoes;
            funcionario.FotoUrl = vm.FotoUrl;
            funcionario.PercentualComissaoPadrao = vm.PercentualComissaoPadrao;
            funcionario.ValorComissaoFixa = vm.ValorComissaoFixa;

            var antigos = await _context.FuncionariosServicos
                .Where(x => x.FuncionarioId == funcionario.Id)
                .ToListAsync();

            _context.FuncionariosServicos.RemoveRange(antigos);

            foreach (var servicoId in vm.ServicosSelecionados ?? new List<int>())
            {
                _context.FuncionariosServicos.Add(new FuncionarioServico
                {
                    FuncionarioId = funcionario.Id,
                    ServicoId = servicoId
                });
            }

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Funcionário atualizado com sucesso.");
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            ToastHelper.Error(TempData, "Erro ao atualizar funcionário.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // TOGGLE ATIVO
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAtivo(int id)
    {
        try
        {
            var empresaId = await GetEmpresaId();

            var funcionario = await _context.Funcionarios
                .FirstOrDefaultAsync(f =>
                    f.Id == id &&
                    f.EmpresaId == empresaId);

            if (funcionario == null)
            {
                ToastHelper.Error(TempData, "Funcionário não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            var possuiAgendamentosFuturos =
                await _context.AgendamentosFuncionarios
                    .Include(a => a.Agendamento)
                    .AnyAsync(a =>
                        a.FuncionarioId == id &&
                        a.Agendamento.DataHora > DateTime.Now);

            if (possuiAgendamentosFuturos && funcionario.Ativo)
            {
                ToastHelper.Warning(TempData,
                    "Existem agendamentos futuros vinculados.");

                return RedirectToAction(nameof(Index));
            }

            funcionario.Ativo = !funcionario.Ativo;

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData,
                funcionario.Ativo ? "Funcionário ativado." : "Funcionário inativado.");

            return RedirectToAction(nameof(Index));
        }
        catch
        {
            ToastHelper.Error(TempData, "Erro ao alterar status.");
            return RedirectToAction(nameof(Index));
        }
    }
}