using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.Enums;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmpresaAgendamento.Controllers
{
    [Route("")]
    public class PublicoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PublicoController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Index(string slug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return NotFound();

                slug = slug.Trim().ToLower();

                var empresa = await _context.Empresas
                    .AsNoTracking()
                    .Include(e => e.Servicos)
                    .FirstOrDefaultAsync(e =>
                        e.Slug.ToLower() == slug &&
                        e.Ativo);

                if (empresa == null)
                    return NotFound();

                var viewModel = new EmpresaPublicaViewModel
                {
                    Empresa = empresa,
                    Servicos = empresa.Servicos
                        .OrderBy(x => x.Nome)
                        .ToList()
                };

                return View(viewModel);
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpGet("Publico/Funcionarios/{empresaId}")]
        public async Task<IActionResult> Funcionarios(int empresaId)
        {
            var funcionarios = await _context.Funcionarios
                .Where(x =>
                    x.EmpresaId == empresaId &&
                    x.Ativo)
                .Select(x => new
                {
                    x.Id,
                    x.Nome
                })
                .ToListAsync();

            return Json(funcionarios);
        }

        [HttpGet("Publico/HorariosDisponiveis")]
        public async Task<IActionResult> HorariosDisponiveis(
       int empresaId,
       int? funcionarioId,
       DateTime data)
        {
            // Horários do dia
            var horarios = new List<string>();

            for (var hora = new TimeSpan(8, 0, 0);
                 hora < new TimeSpan(18, 0, 0);
                 hora += TimeSpan.FromMinutes(30))
            {
                horarios.Add(hora.ToString(@"hh\:mm"));
            }

            // Busca todos os agendamentos do dia em uma única consulta
            var agendamentosDia = await _context.Agendamentos
                .Where(a =>
                    a.EmpresaId == empresaId &&
                    a.DataHora.Date == data.Date &&
                    a.Status != StatusAgendamento.Cancelado)
                .Select(a => new
                {
                    a.FuncionarioId,
                    Hora = a.DataHora.ToString("HH:mm")
                })
                .ToListAsync();

            // Funcionário específico
            if (funcionarioId.HasValue)
            {
                var ocupados = agendamentosDia
                    .Where(a => a.FuncionarioId == funcionarioId)
                    .Select(a => a.Hora)
                    .Distinct()
                    .ToList();

                var disponiveis = horarios
                    .Except(ocupados)
                    .ToList();

                return Json(disponiveis);
            }

            // Qualquer profissional
            var funcionarios = await _context.Funcionarios
                .Where(f =>
                    f.EmpresaId == empresaId &&
                    f.Ativo)
                .Select(f => f.Id)
                .ToListAsync();

            var horariosDisponiveis = new List<string>();

            foreach (var horario in horarios)
            {
                bool existeFuncionarioLivre = funcionarios.Any(funcionario =>
                    !agendamentosDia.Any(a =>
                        a.FuncionarioId == funcionario &&
                        a.Hora == horario));

                if (existeFuncionarioLivre)
                {
                    horariosDisponiveis.Add(horario);
                }
            }

            return Json(horariosDisponiveis);
        }

        [HttpPost("Publico/Agendar")]
        public async Task<IActionResult> Agendar(
     [FromBody] AgendamentoPublicoViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Dados inválidos."
                });

            // Se escolheu "Qualquer profissional"
            if (!model.FuncionarioId.HasValue)
            {
                model.FuncionarioId = await _context.Funcionarios
                    .Where(f =>
                        f.EmpresaId == model.EmpresaId &&
                        f.Ativo)
                    .OrderBy(x => Guid.NewGuid())
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync();

                if (!model.FuncionarioId.HasValue)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Nenhum profissional disponível."
                    });
                }
            }

            // Validação final de conflito
            var conflito = await _context.Agendamentos
                .AnyAsync(a =>
                    a.EmpresaId == model.EmpresaId &&
                    a.FuncionarioId == model.FuncionarioId &&
                    a.DataHora == model.DataHora &&
                    a.Status != StatusAgendamento.Cancelado);

            if (conflito)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este horário acabou de ser ocupado. Escolha outro horário."
                });
            }

            var agendamento = new Agendamento
            {
                EmpresaId = model.EmpresaId,
                ServicoId = model.ServicoId,

                FuncionarioId = model.FuncionarioId,

                DataHora = model.DataHora,

                Status = StatusAgendamento.Agendado,

                ClienteAvulso = true,

                ClienteId = null,

                NomeClienteAvulso = model.Nome,

                TelefoneClienteAvulso = model.Telefone,

                DataCriacao = DateTime.UtcNow,

                Ativo = true
            };

            _context.Agendamentos.Add(agendamento);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                sucesso = true,
                mensagem = "Agendamento realizado com sucesso."
            });
        }
    }
}