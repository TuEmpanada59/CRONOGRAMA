using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Cronograma.Dominio;

/// <summary>Configuración leída de variables de entorno / App Settings de Azure.</summary>
public sealed class Config
{
    public required string DataDir { get; init; }
    public required List<Usuario> Usuarios { get; init; }
    /// <summary>Sin USUARIOS y fuera de Azure/producción: se entra sin clave (solo para probar en local).</summary>
    public bool SinLogin { get; init; }

    public static Config Desde(IConfiguration cfg, IHostEnvironment env)
    {
        var enAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        var dir = cfg["DATA_DIR"] is { Length: > 0 } d ? d
            : enAzure ? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "/home", "data")
            : Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dir);
        var usuarios = Usuario.Leer(cfg["USUARIOS"]);
        return new Config { DataDir = dir, Usuarios = usuarios, SinLogin = usuarios.Count == 0 && !enAzure && env.IsDevelopment() };
    }
}

/// <summary>USUARIOS = "Nombre Uno:claveUno;Nombre Dos:claveDos"</summary>
public sealed record Usuario(string Nombre, string Clave)
{
    public static List<Usuario> Leer(string? valor) => (valor ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(par => par.IndexOf(':') is var i and > 0 ? new Usuario(par[..i].Trim(), par[(i + 1)..]) : null)
        .OfType<Usuario>()
        .Where(u => u.Nombre.Length > 0 && u.Clave.Length > 0)
        .ToList();

    public bool ClaveValida(string intento) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(intento)), SHA256.HashData(Encoding.UTF8.GetBytes(Clave)));
}

/// <summary>Bloquea una IP 15 minutos tras 8 intentos fallidos de ingreso.</summary>
public sealed class LimiteIngreso
{
    readonly ConcurrentDictionary<string, (int n, DateTime t)> intentos = new();
    static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

    public bool Bloqueado(string ip) => intentos.TryGetValue(ip, out var r) && r.n >= 8 && DateTime.UtcNow - r.t < Ventana;
    public void Fallo(string ip) => intentos.AddOrUpdate(ip, (1, DateTime.UtcNow),
        (_, v) => DateTime.UtcNow - v.t > Ventana ? (1, DateTime.UtcNow) : (v.n + 1, v.t));
    public void Exito(string ip) => intentos.TryRemove(ip, out _);
}
