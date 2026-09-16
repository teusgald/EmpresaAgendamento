using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 🔥 DATABASE
// =========================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Azure SQL solta conexão de vez em quando por motivo transitório (rede,
    // failover, throttling) — sem isso, qualquer query nesse instante falha
    // direto em vez de tentar de novo.
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure());

    // Loga valores de parâmetro nas queries — só em Development, nunca em
    // produção (vazaria dados sensíveis nos logs do servidor).
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// =========================
// 🔥 IDENTITY (ÚNICO - SEM DUPLICAÇÃO)
// =========================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;

    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;

    // 🔐 Configuração de senha forte
    options.Password.RequireDigit = true;               // pelo menos 1 número
    options.Password.RequireLowercase = true;           // pelo menos 1 letra minúscula
    options.Password.RequireUppercase = true;           // pelo menos 1 letra maiúscula
    options.Password.RequireNonAlphanumeric = true;    // pelo menos 1 caractere especial (!@#$...)
    options.Password.RequiredLength = 8;               // tamanho mínimo 8
    options.Password.RequiredUniqueChars = 1;          // pelo menos 1 caractere único

    // 🔐 Bloqueio contra força bruta de senha
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>() // necessário para roles
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Sem isso, um usuário logado que tenta acessar algo fora da role dele
// (ex.: Funcionário tentando abrir /Servicos) cai no /Account/AccessDenied
// padrão do Identity, que não existe nesse projeto.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/acesso-negado";
});

// =========================
// 🔥 SERVICES
// =========================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificacaoAgendamentoService, NotificacaoAgendamentoService>();
builder.Services.AddScoped<IPlanoCreditoService, PlanoCreditoService>();
builder.Services.AddScoped<IFinanceiroService, FinanceiroService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddSingleton<IWhatsAppService, WhatsAppService>();
builder.Services.AddHostedService<LembreteAgendamentoBackgroundService>();

// =========================
// 🔥 STRIPE
// =========================
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

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
builder.Services.AddControllersWithViews(options =>
{
    // Global: bloqueia o portal (agendamentos, financeiro, cadastros...)
    // enquanto a empresa não tiver assinatura ativa no Stripe.
    options.Filters.Add<RequerAssinaturaAtivaFilter>();
});
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

    string[] roles = { "Cliente", "Empresa", "Funcionario" };

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

// MODO MANUTENÇÃO — liga via appsettings/variável de ambiente
// ("Manutencao:Ativa": true), sem precisar recompilar. Deixa passar os
// assets (senão a própria tela de manutenção não carrega o CSS) e a rota
// da tela em si, e manda todo o resto pra lá.
app.Use(async (context, next) =>
{
    var manutencaoAtiva = app.Configuration.GetValue<bool>("Manutencao:Ativa");

    var path = context.Request.Path.Value ?? "";

    var isAssetOuManutencao =
        path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/img", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/manutencao", StringComparison.OrdinalIgnoreCase);

    if (manutencaoAtiva && !isAssetOuManutencao)
    {
        context.Response.Redirect("/manutencao");
        return;
    }

    await next();
});

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