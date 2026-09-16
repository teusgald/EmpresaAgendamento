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
}