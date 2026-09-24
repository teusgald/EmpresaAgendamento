using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EmpresaAgendamento.Data;
using EmpresaAgendamento.Filters;
using EmpresaAgendamento.Models;
using EmpresaAgendamento.Services;
using System.Threading.RateLimiting;

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

// =========================
// 🔥 LOGIN COM GOOGLE (Empresa e Cliente)
// =========================
// Duas schemes separadas — cada uma com seu próprio CallbackPath — em vez de
// uma só com discriminador: deixa fisicamente impossível o fluxo Empresa
// acabar criando um Cliente (ou vice-versa). Client ID/Secret são o mesmo
// app OAuth no Google Cloud Console, só com os dois redirect URIs
// autorizados. Configuração via User Secrets/env var, nunca appsettings.json
// (mesmo padrão do WhatsApp:CloudApi:* e AiAssistant:Gemini:ApiKey).
builder.Services.AddAuthentication()
    .AddGoogle("GoogleEmpresa", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/empresa/signin-google";
        options.SignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddGoogle("GoogleCliente", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/cliente/signin-google";
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });

// Sem isso, um usuário logado que tenta acessar algo fora da role dele
// (ex.: Funcionário tentando abrir /Servicos) cai no /Account/AccessDenied
// padrão do Identity, que não existe nesse projeto.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/acesso-negado";

    // Sem isso, o padrão é /Account/Login — que não existe nesse projeto
    // (o login de verdade é via modal da Home ou a tela própria do
    // funcionário) — sessão expirada/deslogado batia em 404 cru.
    options.LoginPath = "/sessao-expirada";

    // 🔐 Sem isso, o padrão é SameAsRequest — o cookie de sessão pode ser
    // mandado numa conexão HTTP se algum endpoint acabar respondendo assim.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// =========================
// 🔥 RATE LIMITING (login, registro, recuperação de senha)
// =========================
// O lockout do Identity (5 tentativas/15min) só entra depois de identificar
// uma conta específica — não impede alguém martelando esses endpoints com
// e-mails diferentes, ou spammando "esqueci senha" (custa envio de e-mail).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(5),
                PermitLimit = 20,
                QueueLimit = 0
            }));
});

// =========================
// 🔥 SERVICES
// =========================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<IEmpresaDescobertaService, EmpresaDescobertaService>();
builder.Services.AddScoped<IClienteUnificacaoService, ClienteUnificacaoService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificacaoAgendamentoService, NotificacaoAgendamentoService>();
builder.Services.AddScoped<IPlanoCreditoService, PlanoCreditoService>();
builder.Services.AddScoped<IFinanceiroService, FinanceiroService>();
builder.Services.AddScoped<INotificacaoService, NotificacaoService>();
builder.Services.AddScoped<IFidelidadeService, FidelidadeService>();
builder.Services.AddScoped<IComandaService, ComandaService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();
builder.Services.AddHostedService<LembreteAgendamentoBackgroundService>();
builder.Services.AddScoped<ISimpliAiToolsService, SimpliAiToolsService>();
builder.Services.AddHttpClient<IAiAssistantService, GeminiAssistantService>();

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

    string[] roles = { "Cliente", "Empresa", "Funcionario", "SuperAdmin" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // =========================
    // 🔥 SEED DO DONO DO SISTEMA (SuperAdmin)
    // =========================
    // Cria o único usuário com acesso ao painel de dono do sistema, se ainda
    // não existir nenhum na role. E-mail/senha vêm de User Secrets
    // (SuperAdminSeed:Email / SuperAdminSeed:Password) — nunca hardcoded,
    // pra não vazar no SVN/Git.
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var jaExisteSuperAdmin = (await userManager.GetUsersInRoleAsync("SuperAdmin")).Any();

    if (!jaExisteSuperAdmin)
    {
        var superAdminEmail = app.Configuration["SuperAdminSeed:Email"];
        var superAdminSenha = app.Configuration["SuperAdminSeed:Password"];

        if (!string.IsNullOrWhiteSpace(superAdminEmail) && !string.IsNullOrWhiteSpace(superAdminSenha))
        {
            var superAdminUser = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                NomeCompleto = "Administrador do Sistema",
                EmailConfirmed = true
            };

            var criarResult = await userManager.CreateAsync(superAdminUser, superAdminSenha);

            if (criarResult.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
            }
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

// 404/403/etc. sem corpo próprio (ex.: return NotFound(), rota que não bate
// com nenhum endpoint) caem aqui em vez de mostrar a página crua do
// servidor — em dev também, pra já testar com a tela de verdade.
app.UseStatusCodePagesWithReExecute("/erro/{0}");

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

app.UseRateLimiter();

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