using EmpresaAgendamento.Models;
using EmpresaAgendamento.Models.ViewModels;
using EmpresaAgendamento.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EmpresaAgendamento.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        // =========================
        // ESQUECI SENHA (GET)
        // =========================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        // =========================
        // ESQUECI SENHA (POST)
        // =========================
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 🔹 Busca todos os usuários com esse email e filtra pelo que é Empresa
            var users = await _userManager.Users
                .Where(u => u.Email == model.Email)
                .ToListAsync();

            ApplicationUser user = null;
            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Empresa"))
                {
                    user = u;
                    break;
                }
            }

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                var link = Url.Action("ResetPassword", "Account",
                    new { userId = user.Id, token = encodedToken },
                    Request.Scheme);

                await _emailService.SendEmailAsync(
                    user.Email,
                    "Redefinir senha",
                    $"Clique aqui para redefinir sua senha: <a href='{link}'>Redefinir senha</a>"
                );
            }

            // 🔒 Mesma resposta exista ou não o usuário, para não revelar quais emails têm conta
            return View("ForgotPasswordConfirmation");
        }

        // =========================
        // RESET SENHA (GET)
        // =========================
        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (userId == null || token == null)
                return RedirectToAction("Login", "Account");

            return View(new ResetPasswordViewModel
            {
                UserId = userId,
                Token = token
            });
        }

        // =========================
        // RESET SENHA (POST)
        // =========================
        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
                return RedirectToAction("ResetPasswordConfirmation");

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
            }
            catch
            {
                ModelState.AddModelError("", "Link de redefinição inválido.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);

            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation");

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // =========================
        // RESET SENHA CONFIRMAÇÃO
        // =========================
        [HttpGet]
        public IActionResult ResetPasswordConfirmation() => View();
    }
}