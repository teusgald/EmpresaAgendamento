using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("PlanosServico")]
    [Authorize(Roles = "Empresa")]
    public class PlanosServicoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly INotificacaoService _notificacaoService;
        private readonly ILogger<PlanosServicoController> _logger;

        public PlanosServicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            INotificacaoService notificacaoService,
            ILogger<PlanosServicoController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _notificacaoService = notificacaoService;
            _logger = logger;
        }

        private async Task<int?> GetEmpresaId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmpresaId;
        }

        // =========================
        // INDEX
        // =========================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var planos = await _context.PlanosServico
                .Include(p => p.Servicos).ThenInclude(x => x.Servico)
                .Include(p => p.Assinaturas)
                .Where(p => p.EmpresaId == empresaId)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return View(planos);
        }

        // =========================
        // CREATE (GET)
        // =========================
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var vm = new PlanoServicoViewModel
            {
                ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value)
            };

            return View(vm);
        }

        // =========================
        // CREATE (POST)
        // =========================
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlanoServicoViewModel vm)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            if (!vm.ServicosSelecionados.Any())
            {
                ModelState.AddModelError("", "Selecione ao menos um serviço incluído no plano.");
            }

            if (!ModelState.IsValid)
            {
                vm.ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value);
                return View(vm);
            }

            var plano = new PlanoServico
            {
                EmpresaId = empresaId.Value,
                Nome = vm.Nome,
                Periodicidade = vm.Periodicidade,
                QuantidadeUsos = vm.QuantidadeUsos,
                ValorReferencia = vm.ValorReferencia,
                PercentualJurosAtraso = vm.PercentualJurosAtraso,
                Ativo = true
            };

            _context.PlanosServico.Add(plano);
            await _context.SaveChangesAsync();

            foreach (var servicoId in vm.ServicosSelecionados)
            {
                _context.PlanosServicoItens.Add(new PlanoServicoItem
                {
                    PlanoServicoId = plano.Id,
                    ServicoId = servicoId
                });
            }

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Plano criado com sucesso!");
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // EDIT (GET)
        // =========================
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var empresaId = await GetEmpresaId();

            var plano = await _context.PlanosServico
                .Include(p => p.Servicos)
                .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

            if (plano == null)
            {
                ToastHelper.Error(TempData, "Plano não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            var vm = new PlanoServicoViewModel
            {
                Id = plano.Id,
                Nome = plano.Nome,
                Periodicidade = plano.Periodicidade,
                QuantidadeUsos = plano.QuantidadeUsos,
                ValorReferencia = plano.ValorReferencia,
                PercentualJurosAtraso = plano.PercentualJurosAtraso,
                Ativo = plano.Ativo,
                ServicosSelecionados = plano.Servicos.Select(x => x.ServicoId).ToList(),
                ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId!.Value)
            };

            return View(vm);
        }

        // =========================
        // EDIT (POST)
        // =========================
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlanoServicoViewModel vm)
        {
            var empresaId = await GetEmpresaId();

            if (empresaId == null)
            {
                ToastHelper.Error(TempData, "Sessão expirada.");
                return RedirectToAction("Login", "Account");
            }

            var plano = await _context.PlanosServico
                .Include(p => p.Servicos)
                .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

            if (plano == null)
            {
                ToastHelper.Error(TempData, "Plano não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            if (!vm.ServicosSelecionados.Any())
            {
                ModelState.AddModelError("", "Selecione ao menos um serviço incluído no plano.");
            }

            if (!ModelState.IsValid)
            {
                vm.ServicosDisponiveis = await CarregarServicosDisponiveisAsync(empresaId.Value);
                return View(vm);
            }

            plano.Nome = vm.Nome;
            plano.Periodicidade = vm.Periodicidade;
            plano.QuantidadeUsos = vm.QuantidadeUsos;
            plano.ValorReferencia = vm.ValorReferencia;
            plano.PercentualJurosAtraso = vm.PercentualJurosAtraso;
            plano.Ativo = vm.Ativo;

            _context.PlanosServicoItens.RemoveRange(plano.Servicos);

            foreach (var servicoId in vm.ServicosSelecionados)
            {
                _context.PlanosServicoItens.Add(new PlanoServicoItem
                {
                    PlanoServicoId = plano.Id,
                    ServicoId = servicoId
                });
            }

            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Plano atualizado com sucesso.");
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // ATIVAR/INATIVAR
        // =========================
        [HttpPost("ToggleAtivo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            var empresaId = await GetEmpresaId();

            var plano = await _context.PlanosServico
                .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

            if (plano == null)
            {
                ToastHelper.Error(TempData, "Plano não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            plano.Ativo = !plano.Ativo;
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, plano.Ativo ? "Plano ativado." : "Plano inativado.");
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // ASSINANTES (LISTA + GERENCIAR)
        // =========================
        [HttpGet("Assinantes/{id}")]
        public async Task<IActionResult> Assinantes(int id)
        {
            var empresaId = await GetEmpresaId();

            var plano = await _context.PlanosServico
                .Include(p => p.Assinaturas).ThenInclude(a => a.Cliente)
                .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId);

            if (plano == null)
            {
                ToastHelper.Error(TempData, "Plano não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            foreach (var assinatura in plano.Assinaturas)
            {
                assinatura.PlanoServico = plano;
                assinatura.AtualizarPeriodoSeNecessario();
            }

            await _context.SaveChangesAsync();

            // EF Core não traduz uma coleção já carregada em memória
            // (plano.Assinaturas) dentro de uma query que vira SQL — por
            // isso extrai os Ids bloqueados antes, num HashSet, e usa
            // Contains (isso sim é traduzido). Era isso que quebrava a
            // página com exceção ao carregar.
            var idsBloqueados = plano.Assinaturas
                .Where(a => a.Status == StatusAssinaturaPlano.Ativa || a.Status == StatusAssinaturaPlano.Pendente)
                .Select(a => a.ClienteId)
                .ToHashSet();

            ViewBag.Clientes = new SelectList(
                await _context.Clientes
                    .Where(c => c.Ativo &&
                        c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId) &&
                        !idsBloqueados.Contains(c.Id))
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");

            return View(plano);
        }

        // =========================
        // ADICIONAR ASSINANTE
        // =========================
        [HttpPost("AdicionarAssinante")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAssinante(int planoServicoId, int clienteId)
        {
            var empresaId = await GetEmpresaId();

            var plano = await _context.PlanosServico
                .FirstOrDefaultAsync(p => p.Id == planoServicoId && p.EmpresaId == empresaId);

            if (plano == null)
            {
                ToastHelper.Error(TempData, "Plano não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            var clienteValido = await _context.Clientes
                .AnyAsync(c => c.Id == clienteId && c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

            if (!clienteValido)
            {
                ToastHelper.Error(TempData, "Cliente não encontrado.");
                return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
            }

            var jaAssinante = await _context.AssinaturasPlanoServico
                .AnyAsync(a =>
                    a.PlanoServicoId == planoServicoId &&
                    a.ClienteId == clienteId &&
                    (a.Status == StatusAssinaturaPlano.Ativa || a.Status == StatusAssinaturaPlano.Pendente));

            if (jaAssinante)
            {
                ToastHelper.Warning(TempData, "Esse cliente já é assinante deste plano.");
                return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
            }

            var agora = DateTime.UtcNow;

            var assinatura = new AssinaturaPlanoServico
            {
                PlanoServicoId = planoServicoId,
                ClienteId = clienteId,
                Status = StatusAssinaturaPlano.Ativa,
                DataInicio = agora,
                DataInicioPeriodoAtual = agora,
                DataAprovacao = agora
            };

            _context.AssinaturasPlanoServico.Add(assinatura);
            await _context.SaveChangesAsync();

            await EnviarEmailPlanoAtivadoSeAplicavelAsync(assinatura.Id);

            ToastHelper.Success(TempData, "Cliente adicionado ao plano.");
            return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
        }

        // =========================
        // APROVAR ASSINATURA (solicitada pelo cliente)
        // =========================
        [HttpPost("AprovarAssinatura")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprovarAssinatura(int id, int planoServicoId)
        {
            var empresaId = await GetEmpresaId();

            var assinatura = await _context.AssinaturasPlanoServico
                .Include(a => a.PlanoServico)
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.PlanoServico.EmpresaId == empresaId &&
                    a.Status == StatusAssinaturaPlano.Pendente);

            if (assinatura == null)
            {
                ToastHelper.Error(TempData, "Solicitação não encontrada.");
                return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
            }

            var agora = DateTime.UtcNow;

            assinatura.Status = StatusAssinaturaPlano.Ativa;
            assinatura.DataAprovacao = agora;
            assinatura.DataInicioPeriodoAtual = agora;
            assinatura.CreditosUsados = 0;

            await _context.SaveChangesAsync();

            await EnviarEmailPlanoAtivadoSeAplicavelAsync(assinatura.Id);

            await _notificacaoService.NotificarClienteAsync(
                assinatura.ClienteId,
                assinatura.PlanoServico.EmpresaId,
                TipoNotificacao.Plano,
                "Plano aprovado",
                $"Seu plano \"{assinatura.PlanoServico.Nome}\" foi aprovado — créditos já liberados.",
                "/Cliente/Planos");

            ToastHelper.Success(TempData, "Assinatura aprovada — créditos liberados pro cliente.");
            return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
        }

        // =========================
        // CANCELAR ASSINATURA (também usado pra recusar uma solicitação
        // ainda pendente — a mensagem pro cliente muda conforme o caso)
        // =========================
        [HttpPost("CancelarAssinatura")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarAssinatura(int id, int planoServicoId)
        {
            var empresaId = await GetEmpresaId();

            var assinatura = await _context.AssinaturasPlanoServico
                .Include(a => a.PlanoServico)
                .FirstOrDefaultAsync(a => a.Id == id && a.PlanoServico.EmpresaId == empresaId);

            if (assinatura == null)
            {
                ToastHelper.Error(TempData, "Assinatura não encontrada.");
                return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
            }

            var eraPendente = assinatura.Status == StatusAssinaturaPlano.Pendente;

            assinatura.Status = StatusAssinaturaPlano.Cancelada;
            assinatura.DataCancelamento = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _notificacaoService.NotificarClienteAsync(
                assinatura.ClienteId,
                assinatura.PlanoServico.EmpresaId,
                TipoNotificacao.Plano,
                eraPendente ? "Pedido de plano recusado" : "Plano cancelado",
                eraPendente
                    ? $"Sua solicitação do plano \"{assinatura.PlanoServico.Nome}\" foi recusada."
                    : $"Seu plano \"{assinatura.PlanoServico.Nome}\" foi cancelado.",
                "/Cliente/Planos");

            ToastHelper.Success(TempData, "Assinatura cancelada.");
            return RedirectToAction(nameof(Assinantes), new { id = planoServicoId });
        }

        // E-mail de "plano ativado" (funciona como recibo/comprovante) — com
        // nome do plano, periodicidade, sessões, valor de referência,
        // vencimento do período e juros de atraso (se a empresa configurou).
        // Uma falha aqui não desfaz a aprovação já salva.
        private async Task EnviarEmailPlanoAtivadoSeAplicavelAsync(int assinaturaId)
        {
            try
            {
                var assinatura = await _context.AssinaturasPlanoServico
                    .Include(a => a.PlanoServico)
                    .Include(a => a.Cliente)
                    .FirstOrDefaultAsync(a => a.Id == assinaturaId);

                var email = assinatura?.Cliente.Email;

                if (assinatura == null || string.IsNullOrWhiteSpace(email))
                    return;

                var plano = assinatura.PlanoServico;

                var periodicidadeTexto = plano.Periodicidade == PeriodicidadePlanoServico.Semanal
                    ? "semanal"
                    : "mensal";

                var linhaValor = plano.ValorReferencia.HasValue
                    ? $"<p><strong>Valor:</strong> R$ {plano.ValorReferencia.Value:F2} ({periodicidadeTexto})</p>"
                    : "";

                var linhaJuros = plano.PercentualJurosAtraso.HasValue
                    ? $"<p><strong>Juros por atraso:</strong> {plano.PercentualJurosAtraso.Value:F2}% sobre o valor do plano</p>"
                    : "";

                await _emailService.SendEmailAsync(
                    email!,
                    $"Plano {plano.Nome} ativado — Simpli Time",
                    $@"
                    <h2>Seu plano foi ativado!</h2>
                    <p>Olá {assinatura.Cliente.Nome}, seu plano <strong>{plano.Nome}</strong> já está ativo.</p>
                    <p><strong>Sessões incluídas:</strong> {plano.QuantidadeUsos} por período ({periodicidadeTexto})</p>
                    {linhaValor}
                    <p><strong>Vencimento do período atual:</strong> {assinatura.DataVencimento:dd/MM/yyyy}</p>
                    {linhaJuros}
                    <p>Isso serve como comprovante da sua contratação. Guarde este e-mail.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de plano ativado (assinatura {AssinaturaId}).", assinaturaId);
            }
        }

        private async Task<List<SelectListItem>> CarregarServicosDisponiveisAsync(int empresaId)
        {
            return await _context.Servicos
                .Where(s => s.EmpresaId == empresaId && s.Ativo)
                .OrderBy(s => s.Nome)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Nome
                })
                .ToListAsync();
        }
    }
}
