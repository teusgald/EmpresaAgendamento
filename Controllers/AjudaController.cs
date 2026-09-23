using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpresaAgendamento.Controllers
{
    // Página de Ajuda (FAQ) — acessível nos três portais (Empresa,
    // Funcionario e Cliente). Uma única view decide o layout e as seções
    // que aparecem, no mesmo padrão já usado por NotificacoesController
    // (Views/Notificacoes/Index.cshtml): não há dado de banco aqui, então
    // a view resolve sozinha (via User.IsInRole / UserManager) o que mostrar,
    // igual o próprio _Layout.cshtml já faz pro menu lateral.
    [Authorize(Roles = "Empresa,Funcionario,Cliente")]
    [Route("ajuda")]
    public class AjudaController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
