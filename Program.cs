// Cronograma · Londoño Gómez
// Blazor (ASP.NET Core, modo interactivo de servidor): interfaz y lógica en .NET.
// Los datos se guardan en un archivo JSON. En Azure App Service la carpeta %HOME%
// (/home en Linux, D:\home en Windows) es persistente: por defecto los datos viven en
// %HOME%/data y sobreviven reinicios y despliegues. No necesita base de datos.

using System.Security.Claims;
using System.Text;
using Cronograma.Components;
using Cronograma.Dominio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var config = Config.Desde(builder.Configuration, builder.Environment);

builder.Services.AddSingleton(config);
builder.Services.AddSingleton<Almacen>();
builder.Services.AddSingleton<LimiteIngreso>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Las claves que firman la cookie de sesión se guardan junto a los datos (sobreviven reinicios)
builder.Services.AddDataProtection()
    .SetApplicationName("cronograma-lg")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(config.DataDir, "claves")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "cg_sesion";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.LoginPath = "/ingresar";
        o.AccessDeniedPath = "/ingresar";
        // Si se quita a alguien de USUARIOS, su sesión deja de valer
        o.Events.OnValidatePrincipal = c =>
        {
            var n = c.Principal?.Identity?.Name;
            if (n is null || !config.Usuarios.Any(u => u.Nombre == n)) c.RejectPrincipal();
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

// Cabeceras de seguridad
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "same-origin";
    h["X-Frame-Options"] = "DENY";
    h["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseAuthentication();
if (config.SinLogin)
    app.Use((ctx, next) =>
    {
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Local")], "local"));
        return next();
    });
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/salud", () => "ok").AllowAnonymous();

app.MapPost("/salir", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/ingresar");
});

// Exportar a CSV (se abre bien en Excel: UTF-8 con BOM y separador ;)
app.MapGet("/exportar.csv", (Almacen db) =>
{
    var d = db.Actual;
    static string Q(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
    var nombreFase = d.Fases.ToDictionary(f => f.Id, f => f.Nombre);
    var orden = d.Fases.Select((f, i) => (f.Id, i)).ToDictionary(x => x.Id, x => x.i);
    var sb = new StringBuilder("\uFEFF");
    sb.AppendLine(string.Join(';', new[] { "Fase", "Tarea", "Responsable", "Inicio", "Entrega", "Estado", "Prioridad", "Avance %", "Subtareas", "Entregable", "Notas" }.Select(Q)));
    foreach (var t in d.Tareas.OrderBy(t => orden.GetValueOrDefault(t.Fase, 999)).ThenBy(t => t.Orden).ThenBy(t => t.Inicio))
        sb.AppendLine(string.Join(';', new object?[] {
            nombreFase.GetValueOrDefault(t.Fase, "Sin fase"), t.Titulo, t.Responsable, t.Inicio, t.Fin,
            Catalogo.Estados.GetValueOrDefault(t.Estado), Catalogo.Prioridades.GetValueOrDefault(t.Prioridad), Math.Round(t.Progreso * 100),
            string.Join(" | ", t.Subtareas.Select(s => (s.Hecha ? "[x] " : "[ ] ") + s.Texto)), t.Entregable, t.Notas }.Select(Q)));
    return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"cronograma-{Fechas.Ymd(Fechas.Hoy)}.csv");
}).RequireAuthorization();

// Respaldo completo en JSON
app.MapGet("/respaldo.json", (Almacen db) =>
    Results.File(db.RespaldoJson(), "application/json", $"cronograma-respaldo-{Fechas.Ymd(Fechas.Hoy)}.json"))
    .RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Logger.LogInformation("Datos en {Dir}", config.DataDir);
if (config.SinLogin) app.Logger.LogWarning("Sin USUARIOS configurados: modo local sin contraseña.");
else if (config.Usuarios.Count == 0) app.Logger.LogWarning("Configura la variable USUARIOS para poder ingresar.");

app.Run();
