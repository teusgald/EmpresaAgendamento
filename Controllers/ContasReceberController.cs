using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Helpers;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("financeiro/contas-a-receber")]
    [Authorize(Roles = "Empresa,Funcionario")]
    [TypeFilter(typeof(RequerPlanoFinanceiroFilter))]
    public class ContasReceberController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFinanceiroService _financeiroService;
        private readonly IEmailService _emailService;
        private readonly INotificacaoService _notificacaoService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ContasReceberController> _logger;

        public ContasReceberController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFinanceiroService financeiroService,
            IEmailService emailService,
            INotificacaoService notificacaoService,
            IConfiguration configuration,
            ILogger<ContasReceberController> logger)
        {
            _context = context;
            _userManager = userManager;
            _financeiroService = financeiroService;
            _emailService = emailService;
            _notificacaoService = notificacaoService;
            _configuration = configuration;
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

        private async Task CarregarCombosAsync(int empresaId)
        {
            ViewBag.Categorias = new SelectList(
                await _context.CategoriasFinanceiras
                    .Where(c =>
                        c.EmpresaId == empresaId &&
                        c.Tipo == TipoCategoriaFinanceira.Receita &&
                        c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");

            ViewBag.Clientes = new SelectList(
                await _context.Clientes
                    .Where(c =>
                        c.Ativo &&
                        c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId))
                    .OrderBy(c => c.Nome)
                    .ToListAsync(),
                "Id", "Nome");
        }

        // =========================
        // LISTAGEM (compartilhada por Contas a Receber e Receitas)
        // =========================
        private async Task<IActionResult> ListarAsync(
            bool somenteAbertas,
            string titulo,
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                const int pageSize = 15;

                var query = _context.ContasReceber
                    .Include(c => c.Cliente)
                    .Include(c => c.Categoria)
                    .Include(c => c.Agendamento)
                    .Where(c => c.EmpresaId == empresaId);

                if (somenteAbertas)
                {
                    query = query.Where(c =>
                        c.Status == StatusConta.Pendente ||
                        c.Status == StatusConta.Parcial);
                }

                if (status.HasValue)
                {
                    query = query.Where(c => c.Status == status.Value);
                }

                if (dataInicial.HasValue)
                {
                    query = query.Where(c => c.DataVencimento >= dataInicial.Value);
                }

                if (dataFinal.HasValue)
                {
                    query = query.Where(c => c.DataVencimento <= dataFinal.Value.AddDays(1));
                }

                query = query.OrderByDescending(c => c.DataVencimento);

                var totalItems = await query.CountAsync();

                var lista = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.Titulo = titulo;
                ViewBag.SomenteAbertas = somenteAbertas;
                ViewBag.Status = status;
                ViewBag.DataInicial = dataInicial;
                ViewBag.DataFinal = dataFinal;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                return View("Index", lista);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar contas a receber.");
                return View("Index", new List<ContaReceber>());
            }
        }

        // Contas a Receber = em aberto (Pendente/Parcial)
        [HttpGet("")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Visualizar" })]
        public Task<IActionResult> Index(
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page = 1)
        {
            return ListarAsync(true, "Contas a Receber", status, dataInicial, dataFinal, page);
        }

        // Receitas = histórico completo (todos os status)
        [HttpGet("/financeiro/receitas")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Visualizar" })]
        public Task<IActionResult> Receitas(
            StatusConta? status,
            DateTime? dataInicial,
            DateTime? dataFinal,
            int page = 1)
        {
            return ListarAsync(false, "Receitas", status, dataInicial, dataFinal, page);
        }

        // =========================
        // CREATE (receita avulsa) GET
        // =========================
        [HttpGet("nova")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Criar" })]
        public async Task<IActionResult> Create()
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                await CarregarCombosAsync(empresaId.Value);

                return View(new ContaReceber
                {
                    DataVencimento = DateTime.Today
                });
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao abrir formulário.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // CREATE (receita avulsa) POST
        // =========================
        [HttpPost("nova")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Criar" })]
        public async Task<IActionResult> Create(ContaReceber conta)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                ModelState.Remove(nameof(ContaReceber.EmpresaId));
                ModelState.Remove(nameof(ContaReceber.Empresa));
                ModelState.Remove(nameof(ContaReceber.Agendamento));
                ModelState.Remove(nameof(ContaReceber.Cliente));
                ModelState.Remove(nameof(ContaReceber.Categoria));
                ModelState.Remove(nameof(ContaReceber.Status));
                ModelState.Remove(nameof(ContaReceber.ValorRecebido));

                if (conta.ValorPrevisto <= 0)
                {
                    ModelState.AddModelError(
                        nameof(ContaReceber.ValorPrevisto),
                        "Informe um valor válido.");
                }

                // EmpresaId nunca vem do formulário: toda referência informada
                // (Categoria/Cliente) é validada contra a empresa logada.
                if (conta.CategoriaId.HasValue)
                {
                    var categoriaValida = await _context.CategoriasFinanceiras
                        .AnyAsync(c =>
                            c.Id == conta.CategoriaId &&
                            c.EmpresaId == empresaId);

                    if (!categoriaValida)
                    {
                        ModelState.AddModelError(
                            nameof(ContaReceber.CategoriaId),
                            "Categoria inválida.");
                    }
                }

                if (conta.ClienteId.HasValue)
                {
                    var clienteValido = await _context.Clientes
                        .AnyAsync(c =>
                            c.Id == conta.ClienteId &&
                            c.EmpresaClientes.Any(ec => ec.EmpresaId == empresaId));

                    if (!clienteValido)
                    {
                        ModelState.AddModelError(
                            nameof(ContaReceber.ClienteId),
                            "Cliente inválido.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    await CarregarCombosAsync(empresaId.Value);
                    return View(conta);
                }

                conta.EmpresaId = empresaId.Value;
                conta.AgendamentoId = null;
                conta.ValorRecebido = 0;
                conta.Status = StatusConta.Pendente;
                conta.DataCriacao = DateTime.UtcNow;

                _context.ContasReceber.Add(conta);
                await _context.SaveChangesAsync();

                ToastHelper.Success(TempData, "Receita lançada com sucesso!");

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao lançar receita.");
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================
        // REGISTRAR RECEBIMENTO (total ou parcial)
        // =========================
        [HttpPost("receber")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Editar" })]
        public async Task<IActionResult> RegistrarRecebimento(
            int id,
            decimal valor,
            FormaPagamento formaPagamento,
            string? origem)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                var usuarioId = _userManager.GetUserId(User);

                var (sucesso, erro, _) = await _financeiroService.RegistrarRecebimentoAsync(
                    id, empresaId.Value, valor, formaPagamento, usuarioId);

                if (!sucesso)
                {
                    ToastHelper.Error(TempData, erro ?? "Erro ao registrar recebimento.");
                }
                else
                {
                    ToastHelper.Success(TempData, "Recebimento registrado com sucesso!");
                    await EnviarReciboPorEmailAsync(id);

                    await _notificacaoService.NotificarEmpresaAsync(
                        empresaId.Value,
                        TipoNotificacao.Pagamento,
                        "Pagamento recebido",
                        $"Recebimento de {valor:C} registrado.",
                        "/financeiro/contas-a-receber");

                    var clienteId = await _context.ContasReceber
                        .Where(c => c.Id == id)
                        .Select(c => c.ClienteId)
                        .FirstOrDefaultAsync();

                    if (clienteId.HasValue)
                    {
                        await _notificacaoService.NotificarClienteAsync(
                            clienteId.Value,
                            empresaId.Value,
                            TipoNotificacao.Pagamento,
                            "Pagamento confirmado",
                            $"Recebemos seu pagamento de {valor:C}.",
                            null);
                    }
                }

                return RedirectToAction(origem == "receitas" ? nameof(Receitas) : nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao registrar recebimento.");
                return RedirectToAction(origem == "receitas" ? nameof(Receitas) : nameof(Index));
            }
        }

        // Manda o link do recibo pro cliente por e-mail — só quando ele tem
        // e-mail cadastrado (cliente avulso não tem). Nunca bloqueia o
        // recebimento em si por causa de falha no envio.
        private async Task EnviarReciboPorEmailAsync(int contaId)
        {
            try
            {
                var conta = await _context.ContasReceber
                    .Include(c => c.Cliente)
                    .Include(c => c.Empresa)
                    .FirstOrDefaultAsync(c => c.Id == contaId);

                if (string.IsNullOrWhiteSpace(conta?.Cliente?.Email))
                    return;

                var nomeEmpresa = conta.Empresa.NomeFantasia ?? conta.Empresa.Nome;
                var link = $"{LinkBaseHelper.ObterBase(Request, _configuration)}/recibo/{conta.ReciboToken}";

                await _emailService.SendEmailAsync(
                    conta.Cliente.Email,
                    $"Recibo de pagamento — {nomeEmpresa}",
                    $@"
                    <h2>Pagamento confirmado</h2>
                    <p>Olá {conta.Cliente.Nome}, recebemos seu pagamento de {conta.ValorRecebido:C} referente a {conta.Descricao}.</p>
                    <p><a href='{link}'>Clique aqui para ver ou imprimir seu recibo</a></p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar recibo por e-mail da conta {ContaId}.", contaId);
            }
        }

        // =========================
        // CANCELAR / ESTORNAR
        // =========================
        [HttpPost("cancelar")]
        [ValidateAntiForgeryToken]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Excluir" })]
        public async Task<IActionResult> Cancelar(int id, string motivo, string? origem)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                if (string.IsNullOrWhiteSpace(motivo))
                {
                    motivo = "Cancelado manualmente.";
                }

                var (sucesso, erro) = await _financeiroService.CancelarContaReceberAsync(
                    id, empresaId.Value, motivo);

                if (!sucesso)
                {
                    ToastHelper.Error(TempData, erro ?? "Erro ao cancelar.");
                }
                else
                {
                    ToastHelper.Success(TempData, "Conta cancelada/estornada com sucesso.");
                }

                return RedirectToAction(origem == "receitas" ? nameof(Receitas) : nameof(Index));
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao cancelar conta.");
                return RedirectToAction(origem == "receitas" ? nameof(Receitas) : nameof(Index));
            }
        }

        // =========================
        // RECIBO (impressão do navegador — sem valor fiscal)
        // =========================
        [HttpGet("{id}/recibo")]
        [TypeFilter(typeof(RequerPermissaoFilter), Arguments = new object[] { "ContasReceber", "Visualizar" })]
        public async Task<IActionResult> Recibo(int id)
        {
            try
            {
                var empresaId = await GetEmpresaId();

                if (empresaId == null)
                {
                    ToastHelper.Error(TempData, "Sessão expirada.");
                    return RedirectToAction("Login", "EmpresaAuth");
                }

                var conta = await _context.ContasReceber
                    .Include(c => c.Cliente)
                    .Include(c => c.Agendamento).ThenInclude(a => a!.Servico)
                    .Include(c => c.Agendamento).ThenInclude(a => a!.ItensComanda).ThenInclude(i => i.Produto)
                    .Include(c => c.Empresa)
                    .FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId);

                if (conta == null)
                {
                    ToastHelper.Error(TempData, "Conta não encontrada.");
                    return RedirectToAction(nameof(Index));
                }

                if (conta.ValorRecebido <= 0)
                {
                    ToastHelper.Warning(TempData, "Essa conta ainda não teve nenhum recebimento — não há o que emitir recibo.");
                    return RedirectToAction(nameof(Index));
                }

                return View(conta);
            }
            catch
            {
                ToastHelper.Error(TempData, "Erro ao carregar recibo.");
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
