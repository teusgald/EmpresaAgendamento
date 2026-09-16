using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    // Página inicial pública
    [HttpGet("/")]
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/termos-de-uso")]
    [AllowAnonymous]
    public IActionResult Termos()
    {
        return View();
    }

    [HttpGet("/politica-de-privacidade")]
    [AllowAnonymous]
    public IActionResult Privacidade()
    {
        return View();
    }

    // Destino do AccessDeniedPath (ver Program.cs) — quando um usuário
    // logado tenta acessar algo que não é da role dele (ex.: Funcionário
    // tentando entrar em Serviços/Financeiro). Sem essa página, o Identity
    // cai no /Account/AccessDenied padrão, que não existe nesse projeto.
    [HttpGet("/acesso-negado")]
    [AllowAnonymous]
    public IActionResult AcessoNegado()
    {
        return View();
    }
}