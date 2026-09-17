using EmpresaAgendamento.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

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

    // Destino do LoginPath (ver Program.cs) — quando a sessão expira (ou o
    // usuário desloga e volta pra uma tela do portal), o Identity manda pra
    // cá em vez do /Account/Login padrão, que também não existe nesse
    // projeto (o login de verdade é feito pelos modais desta Home, ou pela
    // tela própria do funcionário) — sem isso, a pessoa caía num 404 cru.
    [HttpGet("/sessao-expirada")]
    [AllowAnonymous]
    public IActionResult SessaoExpirada(string? returnUrl)
    {
        var url = returnUrl ?? "";

        if (url.StartsWith("/funcionario", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("/funcionario/login?sessaoExpirada=true");
        }

        var tipo = url.StartsWith("/Cliente", StringComparison.OrdinalIgnoreCase) ? "cliente" : "empresa";

        return Redirect($"/?login=true&type={tipo}&sessaoExpirada=true");
    }

    // Destino do app.UseExceptionHandler("/Home/Error") em produção (ver
    // Program.cs) — sem essa ação, um erro não tratado batia numa rota que
    // não existia e o usuário via só uma tela em branco/erro cru do servidor.
    [HttpGet("/Home/Error")]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    // Tela mostrada quando "Manutencao:Ativa" está ligado no appsettings
    // (ver o middleware em Program.cs) — todo o resto do site redireciona
    // pra cá enquanto estiver ativo.
    [HttpGet("/manutencao")]
    [AllowAnonymous]
    public IActionResult Manutencao()
    {
        return View();
    }
}