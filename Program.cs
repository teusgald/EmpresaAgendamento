using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Data;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 🔥 DATABASE
// =========================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging());

// =========================
// 🔥 IDENTITY (ÚNICO - SEM DUPLICAÇÃO)
// =========================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = false;

    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;

    // 🔐 Configuração de senha forte
    options.Password.RequireDigit = true;               // pelo menos 1 número
    options.Password.RequireLowercase = true;           // pelo menos 1 letra minúscula
    options.Password.RequireUppercase = true;           // pelo menos 1 letra maiúscula
    options.Password.RequireNonAlphanumeric = true;    // pelo menos 1 caractere especial (!@#$...)
    options.Password.RequiredLength = 8;               // tamanho mínimo 8
    options.Password.RequiredUniqueChars = 1;          // pelo menos 1 caractere único
})
.AddRoles<IdentityRole>() // necessário para roles
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// =========================
// 🔥 SERVICES
// =========================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// =========================
// 🔥 SESSION
// =========================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =========================
// 🔥 MVC + RAZOR
// =========================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// =========================
// 🚀 BUILD
// =========================
var app = builder.Build();

// =========================
// 🔥 CRIAR ROLES AUTOMATICAMENTE
// =========================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Cliente", "Empresa" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

// =========================
// 🔥 PIPELINE
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// =========================
// 🔥 ROTAS
// =========================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();