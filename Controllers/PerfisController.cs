using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    // Gestão de perfil de acesso é exclusiva do dono da empresa — nenhum
    // funcionário, nem Gerente, define o próprio nível de permissão.
    [Route("perfis")]
    [Authorize(Roles = "Empresa")]
    public class PerfisController : Controller
    {
        // Módulo que ainda não tem RequerPermissaoFilter aplicado no
        // controller real (ver plano Fase 2) continua funcionando como
        // antes — cadastrar permissão aqui não tem efeito até o controller
        // daquele módulo ser migrado do RequerGerenteFilter.
        private static readonly (string Chave, string Label)[] Modulos =
        {
            ("Clientes", "Clientes"),
            ("Servicos", "Serviços"),
            ("Produtos", "Produtos"),
            ("Financeiro", "Financeiro"),
            ("Fidelidade", "Fidelidade"),
            ("ContasReceber", "Contas a Receber"),
            ("ContasPagar", "Contas a Pagar"),
            ("Comissoes", "Comissões"),
            ("CategoriasFinanceiras", "Categorias Financeiras")
        };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PerfisController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var perfis = await _context.Perfis
                .Where(p => p.EmpresaId == empresaId)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return View(perfis);
        }

        [HttpGet("criar")]
        public IActionResult Create()
        {
            var model = new PerfilFormViewModel
            {
                Permissoes = Modulos
                    .Select(m => new PerfilPermissaoItemViewModel { Modulo = m.Chave, ModuloLabel = m.Label })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost("criar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PerfilFormViewModel model)
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
                    ToastHelper.Warning(TempData, "Preencha o nome do perfil.");
                    return View(model);
                }

                var perfil = new Perfil { EmpresaId = empresaId.Value, Nome = model.Nome };

                _context.Perfis.Add(perfil);
                await _context.SaveChangesAsync();

                foreach (var item in model.Permissoes)
                {
                    _context.PerfilPermissoes.Add(new PerfilPermissao
                    {
                        PerfilId = perfil.Id,
                        Modulo = item.Modulo,
                        PodeVisualizar = item.PodeVisualizar,
                        PodeCriar = item.PodeCriar,
                        PodeEditar = item.PodeEditar,
                        PodeExcluir = item.PodeExcluir,
                        EscopoDados = item.EscopoDados
                    });
                }

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Perfil criado com sucesso.");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ToastHelper.Error(TempData, "Erro ao criar perfil.");
                return View(model);
            }
        }

        [HttpGet("{id:int}/editar")]
        public async Task<IActionResult> Edit(int id)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var perfil = await _context.Perfis
                .Include(p => p.Permissoes)
                .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

            if (perfil == null)
            {
                ToastHelper.Error(TempData, "Perfil não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            var model = new PerfilFormViewModel
            {
                Id = perfil.Id,
                Nome = perfil.Nome,
                Permissoes = Modulos.Select(m =>
                {
                    var existente = perfil.Permissoes.FirstOrDefault(p => p.Modulo == m.Chave);

                    return new PerfilPermissaoItemViewModel
                    {
                        Modulo = m.Chave,
                        ModuloLabel = m.Label,
                        PodeVisualizar = existente?.PodeVisualizar ?? false,
                        PodeCriar = existente?.PodeCriar ?? false,
                        PodeEditar = existente?.PodeEditar ?? false,
                        PodeExcluir = existente?.PodeExcluir ?? false,
                        EscopoDados = existente?.EscopoDados ?? Models.Enums.EscopoDadosPerfil.Todos
                    };
                }).ToList()
            };

            return View(model);
        }

        [HttpPost("{id:int}/editar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PerfilFormViewModel model)
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
                    ToastHelper.Warning(TempData, "Preencha o nome do perfil.");
                    return View(model);
                }

                var perfil = await _context.Perfis
                    .Include(p => p.Permissoes)
                    .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

                if (perfil == null)
                {
                    ToastHelper.Error(TempData, "Perfil não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                perfil.Nome = model.Nome;

                foreach (var item in model.Permissoes)
                {
                    var existente = perfil.Permissoes.FirstOrDefault(p => p.Modulo == item.Modulo);

                    if (existente == null)
                    {
                        _context.PerfilPermissoes.Add(new PerfilPermissao
                        {
                            PerfilId = perfil.Id,
                            Modulo = item.Modulo,
                            PodeVisualizar = item.PodeVisualizar,
                            PodeCriar = item.PodeCriar,
                            PodeEditar = item.PodeEditar,
                            PodeExcluir = item.PodeExcluir,
                            EscopoDados = item.EscopoDados
                        });
                    }
                    else
                    {
                        existente.PodeVisualizar = item.PodeVisualizar;
                        existente.PodeCriar = item.PodeCriar;
                        existente.PodeEditar = item.PodeEditar;
                        existente.PodeExcluir = item.PodeExcluir;
                        existente.EscopoDados = item.EscopoDados;
                    }
                }

                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Perfil atualizado com sucesso.");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ToastHelper.Error(TempData, "Erro ao atualizar perfil.");
                return View(model);
            }
        }

        [HttpPost("{id:int}/toggle-ativo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var perfil = await _context.Perfis.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

            if (perfil == null)
            {
                ToastHelper.Error(TempData, "Perfil não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            perfil.Ativo = !perfil.Ativo;
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, perfil.Ativo ? "Perfil ativado." : "Perfil desativado.");
            return RedirectToAction(nameof(Index));
        }
    }
}
