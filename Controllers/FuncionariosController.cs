using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
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
    private readonly IEmailService _emailService;
    private readonly ILogger<FuncionariosController> _logger;

    public FuncionariosController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<FuncionariosController> logger)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
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

            var limite = await _context.Empresas
                .Where(e => e.Id == empresaId)
                .Select(e => e.Plano != null ? e.Plano.LimiteFuncionarios : 0)
                .FirstOrDefaultAsync();

            if (limite > 0)
            {
                var totalAtivos = await _context.Funcionarios
                    .CountAsync(f => f.EmpresaId == empresaId && f.Ativo);

                if (totalAtivos >= limite)
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

                    ToastHelper.Warning(
                        TempData,
                        $"Seu plano permite até {limite} funcionário(s) ativo(s). Inative algum ou faça upgrade do plano.");

                    return View(vm);
                }
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

    // =========================
    // CRIAR ACESSO (LOGIN DO FUNCIONÁRIO)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarAcesso(int id)
    {
        var empresaId = await GetEmpresaId();

        if (empresaId == null)
        {
            ToastHelper.Error(TempData, "Sessão expirada.");
            return RedirectToAction("Login", "Account");
        }

        var funcionario = await _context.Funcionarios
            .FirstOrDefaultAsync(f => f.Id == id && f.EmpresaId == empresaId);

        if (funcionario == null)
        {
            ToastHelper.Error(TempData, "Funcionário não encontrado.");
            return RedirectToAction(nameof(Index));
        }

        if (funcionario.UserId != null)
        {
            ToastHelper.Warning(TempData, "Este funcionário já possui acesso.");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(funcionario.Email))
        {
            ToastHelper.Warning(TempData, "Cadastre um e-mail para o funcionário antes de criar o acesso.");
            return RedirectToAction(nameof(Index));
        }

        var emailEmUso = await _userManager.Users.AnyAsync(u => u.Email == funcionario.Email);

        if (emailEmUso)
        {
            ToastHelper.Error(TempData, "Já existe uma conta cadastrada com este e-mail.");
            return RedirectToAction(nameof(Index));
        }

        var user = new ApplicationUser
        {
            UserName = $"funcionario-{Guid.NewGuid()}",
            Email = funcionario.Email,
            NomeCompleto = funcionario.Nome,
            PhoneNumber = funcionario.Telefone,
            EmpresaId = empresaId,
            EmailConfirmed = true
        };

        // Senha aleatória descartável — o funcionário nunca a vê, ele define
        // a própria senha pelo link enviado por e-mail (ResetPasswordAsync).
        var senhaDescartavel = Guid.NewGuid().ToString("N") + "Aa1!";

        var result = await _userManager.CreateAsync(user, senhaDescartavel);

        if (!result.Succeeded)
        {
            ToastHelper.Error(
                TempData,
                string.Join(" ", result.Errors.Select(x => x.Description)));

            return RedirectToAction(nameof(Index));
        }

        await _userManager.AddToRoleAsync(user, "Funcionario");

        funcionario.UserId = user.Id;
        await _context.SaveChangesAsync();

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var link =
                $"{Request.Scheme}://{Request.Host}/funcionario/definir-senha" +
                $"?email={Uri.EscapeDataString(user.Email)}" +
                $"&token={Uri.EscapeDataString(token)}";

            await _emailService.SendEmailAsync(
                user.Email,
                "Seu acesso ao Simpli Time",
                $@"
                <h2>Olá {funcionario.Nome}</h2>
                <p>Você agora tem acesso ao sistema de agendamentos da empresa.</p>
                <p><a href='{link}'>Clique aqui para definir sua senha</a></p>
                <p>Depois de definir a senha, entre em <a href='{Request.Scheme}://{Request.Host}/funcionario/login'>{Request.Scheme}://{Request.Host}/funcionario/login</a>.</p>");

            ToastHelper.Success(TempData, "Acesso criado! Enviamos um e-mail para o funcionário definir a senha.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail de acesso pro funcionário {FuncionarioId}.", funcionario.Id);
            ToastHelper.Warning(TempData, "Acesso criado, mas não foi possível enviar o e-mail. Tente reenviar mais tarde ou repasse a senha manualmente.");
        }

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // HORÁRIOS DE TRABALHO (GET)
    // =========================
    [HttpGet]
    public async Task<IActionResult> Horarios(int id)
    {
        var empresaId = await GetEmpresaId();

        if (empresaId == null)
        {
            ToastHelper.Error(TempData, "Sessão expirada.");
            return RedirectToAction("Login", "Account");
        }

        var funcionario = await _context.Funcionarios
            .Include(f => f.Horarios)
            .FirstOrDefaultAsync(f => f.Id == id && f.EmpresaId == empresaId);

        if (funcionario == null)
        {
            ToastHelper.Error(TempData, "Funcionário não encontrado.");
            return RedirectToAction(nameof(Index));
        }

        var lista = new List<FuncionarioHorarioItemViewModel>();

        foreach (DayOfWeek dia in Enum.GetValues<DayOfWeek>())
        {
            var existente = funcionario.Horarios.FirstOrDefault(h => h.DiaSemana == dia);

            lista.Add(new FuncionarioHorarioItemViewModel
            {
                DiaSemana = dia,
                TrabalhaNoDia = existente?.TrabalhaNoDia ?? (dia != DayOfWeek.Sunday),
                HoraInicio = existente?.HoraInicio ?? new TimeSpan(8, 0, 0),
                HoraFim = existente?.HoraFim ?? new TimeSpan(18, 0, 0),
                InicioIntervalo = existente?.InicioIntervalo,
                FimIntervalo = existente?.FimIntervalo
            });
        }

        var horariosEmpresa = await _context.EmpresasHorarios
            .Where(h => h.EmpresaId == empresaId)
            .ToDictionaryAsync(h => h.DiaSemana);

        ViewBag.FuncionarioId = funcionario.Id;
        ViewBag.FuncionarioNome = funcionario.Nome;
        ViewBag.JaConfigurado = funcionario.Horarios.Any();
        ViewBag.HorariosEmpresa = horariosEmpresa;

        return View(lista);
    }

    // Confere se o expediente do funcionário cabe dentro do horário de
    // funcionamento da empresa nesse dia. Empresa sem horário configurado
    // ainda (dicionário vazio) não bloqueia nada — comportamento antigo.
    private static string? ValidarDentroDoExpedienteEmpresa(
        FuncionarioHorarioItemViewModel item,
        Dictionary<DayOfWeek, EmpresaHorario> horariosEmpresa)
    {
        if (!item.TrabalhaNoDia || horariosEmpresa.Count == 0)
            return null;

        if (!horariosEmpresa.TryGetValue(item.DiaSemana, out var horarioEmpresa))
            return null;

        var nomeDia = item.DiaSemana switch
        {
            DayOfWeek.Sunday => "domingo",
            DayOfWeek.Monday => "segunda-feira",
            DayOfWeek.Tuesday => "terça-feira",
            DayOfWeek.Wednesday => "quarta-feira",
            DayOfWeek.Thursday => "quinta-feira",
            DayOfWeek.Friday => "sexta-feira",
            DayOfWeek.Saturday => "sábado",
            _ => item.DiaSemana.ToString()
        };

        if (!horarioEmpresa.TrabalhaNoDia)
            return $"A empresa não abre {nomeDia}, então o funcionário não pode trabalhar nesse dia.";

        if (item.HoraInicio is null || item.HoraFim is null)
            return null;

        if (item.HoraInicio < horarioEmpresa.HoraInicio || item.HoraFim > horarioEmpresa.HoraFim)
        {
            return $"O horário de {nomeDia} precisa estar dentro do funcionamento da empresa " +
                   $"({horarioEmpresa.HoraInicio:hh\\:mm} – {horarioEmpresa.HoraFim:hh\\:mm}).";
        }

        return null;
    }

    // =========================
    // HORÁRIOS DE TRABALHO (POST)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Horarios(int id, List<FuncionarioHorarioItemViewModel> horarios)
    {
        var empresaId = await GetEmpresaId();

        if (empresaId == null)
        {
            ToastHelper.Error(TempData, "Sessão expirada.");
            return RedirectToAction("Login", "Account");
        }

        var funcionario = await _context.Funcionarios
            .FirstOrDefaultAsync(f => f.Id == id && f.EmpresaId == empresaId);

        if (funcionario == null)
        {
            ToastHelper.Error(TempData, "Funcionário não encontrado.");
            return RedirectToAction(nameof(Index));
        }

        horarios ??= new List<FuncionarioHorarioItemViewModel>();

        var horariosEmpresa = await _context.EmpresasHorarios
            .Where(h => h.EmpresaId == empresaId)
            .ToDictionaryAsync(h => h.DiaSemana);

        foreach (var item in horarios)
        {
            var erro = ValidarDentroDoExpedienteEmpresa(item, horariosEmpresa);

            if (erro != null)
            {
                ToastHelper.Error(TempData, erro);

                ViewBag.FuncionarioId = funcionario.Id;
                ViewBag.FuncionarioNome = funcionario.Nome;
                ViewBag.JaConfigurado = true;
                ViewBag.HorariosEmpresa = horariosEmpresa;

                return View(horarios);
            }
        }

        var existentes = await _context.FuncionariosHorarios
            .Where(h => h.FuncionarioId == id)
            .ToListAsync();

        _context.FuncionariosHorarios.RemoveRange(existentes);

        foreach (var item in horarios)
        {
            _context.FuncionariosHorarios.Add(new FuncionarioHorario
            {
                FuncionarioId = id,
                DiaSemana = item.DiaSemana,
                TrabalhaNoDia = item.TrabalhaNoDia,
                HoraInicio = item.HoraInicio ?? TimeSpan.Zero,
                HoraFim = item.HoraFim ?? TimeSpan.Zero,
                InicioIntervalo = item.TrabalhaNoDia ? item.InicioIntervalo : null,
                FimIntervalo = item.TrabalhaNoDia ? item.FimIntervalo : null
            });
        }

        await _context.SaveChangesAsync();

        ToastHelper.Success(TempData, "Horários de trabalho atualizados com sucesso!");
        return RedirectToAction(nameof(Index));
    }
}