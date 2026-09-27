using ChatApp.Data;
using ChatApp.Hubs;
using ChatApp.Models;
using ChatApp.Services;
using ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CHAVES DE PROTEÇÃO DE DADOS
// ============================================================
var keyPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("ChatApp");

// ============================================================
// BASE DE DADOS — SQL SERVER
// ============================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ============================================================
// IDENTITY — CONTAS, AUTENTICAÇÃO E AUTORIZAÇÃO
// ============================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;

    // Ativar em produção quando o envio de e-mail estiver configurado.
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ============================================================
// COOKIE DE AUTENTICAÇÃO
// ============================================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";

    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;

    // Faz o cookie de autenticação funcionar
    // corretamente com os Hubs SignalR.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);

        return Task.CompletedTask;
    };
});

// ============================================================
// MVC + SIGNALR
// ============================================================
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 256 * 1024;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("non-api");

        var key = context.User.Identity?.IsAuthenticated == true
            ? $"user:{context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

// ============================================================
// INJEÇÃO DE DEPENDÊNCIA — SERVIÇOS DA APLICAÇÃO
// ============================================================

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IFileStorageService, FileStorageService>();

builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IChannelService, ChannelService>();

builder.Services.AddScoped<IPostService, PostService>();

builder.Services.AddScoped<IChannelInviteService, ChannelInviteService>();

// Serviço responsável por aplicar as regras de privacidade
// definidas pelo utilizador.
builder.Services.AddScoped<IPrivacyService, PrivacyService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddHostedService<DbSchemaInitializer>();

// ============================================================
// CONSTRUÇÃO DA APLICAÇÃO
// ============================================================
var app = builder.Build();

// Aplicar migrations existentes antes da inicialização do schema adicional.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// ============================================================
// PIPELINE DE PRODUÇÃO
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

// ============================================================
// HTTPS + SECURITY HEADERS
// ============================================================
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(self), geolocation=()";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
    await next();
});

// ============================================================
// FICHEIROS ESTÁTICOS
// ============================================================
app.UseStaticFiles();

// ============================================================
// ROUTING
// ============================================================
app.UseRouting();

app.UseRateLimiter();

// ============================================================
// AUTENTICAÇÃO
// ============================================================
app.UseAuthentication();

// ============================================================
// AUTORIZAÇÃO
// ============================================================
app.UseAuthorization();

// ============================================================
// ROTA MVC PRINCIPAL
// ============================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

// ============================================================
// SIGNALR — CHAT
// ============================================================
app.MapHub<ChatHub>("/hubs/chat");

// ============================================================
// SIGNALR — CHAMADAS
// ============================================================
app.MapHub<CallHub>("/hubs/call");
app.MapHub<GroupHub>("/hubs/group");

// ============================================================
// INICIAR A APLICAÇÃO
// ============================================================
app.Run();
