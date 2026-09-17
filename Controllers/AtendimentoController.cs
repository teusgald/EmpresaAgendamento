using EmpresaAgendamento.Data;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    // Aberto pra qualquer papel logado (Empresa, Funcionario ou Cliente) —
    // o link aparece nos portais dos três, todos falando com o mesmo e-mail
    // de atendimento.
    [Authorize]
    [Route("atendimento")]
    public class AtendimentoController : Controller
    {
        private const string EmailAtendimento = "atendimento.simplitime@sistemateus.com";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<AtendimentoController> _logger;

        public AtendimentoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ILogger<AtendimentoController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var model = await MontarModeloComUsuarioAsync();
            ViewBag.Lista = await CarregarListaAsync();
            return View(model);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Index(AtendimentoViewModel model)
        {
            // Nome/Email/Origem sempre vêm do usuário logado, nunca do que foi
            // digitado no form — evita que alguém forje remetente.
            var modeloAtual = await MontarModeloComUsuarioAsync();
            model.Nome = modeloAtual.Nome;
            model.Email = modeloAtual.Email;
            model.Origem = modeloAtual.Origem;

            ModelState.Remove(nameof(AtendimentoViewModel.Nome));
            ModelState.Remove(nameof(AtendimentoViewModel.Email));

            if (!ModelState.IsValid)
            {
                ViewBag.Lista = await CarregarListaAsync();
                return View(model);
            }

            var usuarioId = _userManager.GetUserId(User);

            var atendimento = new Atendimento
            {
                UsuarioId = usuarioId,
                Nome = model.Nome,
                Email = model.Email,
                Origem = model.Origem,
                Assunto = model.Assunto,
                Mensagem = model.Mensagem,
                Status = StatusAtendimento.Aguardando
            };

            _context.Atendimentos.Add(atendimento);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    EmailAtendimento,
                    $"[Atendimento Simpli Time] {model.Assunto}",
                    $@"
                    <h2>Nova solicitação de atendimento</h2>
                    <p><strong>De:</strong> {model.Nome} ({model.Origem})</p>
                    <p><strong>E-mail para retorno:</strong> {model.Email}</p>
                    <p><strong>Assunto:</strong> {model.Assunto}</p>
                    <p><strong>Mensagem:</strong></p>
                    <p>{model.Mensagem.Replace("\n", "<br>")}</p>");

                ToastHelper.Success(TempData, "Mensagem enviada! Nossa equipe vai te responder em breve.");
            }
            catch (Exception ex)
            {
                // A solicitação já ficou salva (e visível na lista) mesmo se o
                // e-mail falhar — só avisa que o envio não confirmou.
                _logger.LogError(ex, "Falha ao enviar e-mail da solicitação de atendimento {Id}.", atendimento.Id);
                ToastHelper.Warning(TempData, "Sua solicitação foi registrada, mas não conseguimos confirmar o envio do e-mail agora.");
            }

            return RedirectToAction(nameof(Index));
        }

        // O próprio usuário marca como resolvido (não existe painel de
        // atendimento interno ainda — é o jeito mais simples de fechar por ora).
        [HttpPost("concluir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Concluir(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            var atendimento = await _context.Atendimentos
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId);

            if (atendimento == null)
            {
                ToastHelper.Error(TempData, "Atendimento não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            atendimento.Status = StatusAtendimento.Finalizado;
            atendimento.DataFinalizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            ToastHelper.Success(TempData, "Atendimento marcado como concluído.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<Atendimento>> CarregarListaAsync()
        {
            var usuarioId = _userManager.GetUserId(User);

            return await _context.Atendimentos
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.DataCriacao)
                .ToListAsync();
        }

        private async Task<AtendimentoViewModel> MontarModeloComUsuarioAsync()
        {
            var model = new AtendimentoViewModel();

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return model;

            model.Email = user.Email ?? "";

            if (await _userManager.IsInRoleAsync(user, "Funcionario"))
            {
                var funcionario = await _context.Funcionarios
                    .FirstOrDefaultAsync(f => f.UserId == user.Id);

                model.Nome = funcionario?.Nome ?? user.NomeCompleto ?? "";
                model.Origem = "Funcionário";
            }
            else if (user.ClienteId.HasValue)
            {
                var cliente = await _context.Clientes.FindAsync(user.ClienteId.Value);

                model.Nome = cliente?.Nome ?? user.NomeCompleto ?? "";
                model.Origem = "Cliente";
            }
            else if (user.EmpresaId.HasValue)
            {
                var empresa = await _context.Empresas.FindAsync(user.EmpresaId.Value);

                model.Nome = empresa?.NomeFantasia ?? empresa?.Nome ?? user.NomeCompleto ?? "";
                model.Origem = "Empresa";
            }

            return model;
        }
    }
}
