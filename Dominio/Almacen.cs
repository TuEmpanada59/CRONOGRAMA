using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Cronograma.Dominio;

/// <summary>
/// Guarda todo el cronograma en un único archivo JSON (cronograma.json). No usa base de datos.
/// Pensado para un equipo pequeño y una sola instancia de la app:
/// escrituras en serie, guardado atómico (archivo temporal + reemplazo) y respaldo diario (30 días).
/// Los datos publicados en <see cref="Actual"/> nunca se modifican: cada escritura crea una copia nueva,
/// así los componentes pueden leerlos sin bloqueos. <see cref="Cambio"/> avisa a todas las pantallas abiertas.
/// </summary>
public sealed class Almacen
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly string archivo, respaldos;
    readonly SemaphoreSlim candado = new(1, 1);
    readonly ILogger<Almacen> log;

    public Datos Actual { get; private set; }
    public event Action? Cambio;

    public Almacen(Config config, ILogger<Almacen> log)
    {
        this.log = log;
        archivo = Path.Combine(config.DataDir, "cronograma.json");
        respaldos = Path.Combine(config.DataDir, "respaldos");
        Directory.CreateDirectory(respaldos);
        Actual = Cargar(config.Usuarios.Select(u => u.Nombre).ToList());
    }

    Datos Cargar(List<string> equipoInicial)
    {
        if (File.Exists(archivo))
        {
            try
            {
                var d = JsonSerializer.Deserialize<Datos>(File.ReadAllText(archivo), Json) ?? throw new JsonException("vacío");
                d.Fases ??= []; d.Tareas ??= []; d.Equipo ??= [];
                foreach (var t in d.Tareas) t.Subtareas ??= [];
                return d;
            }
            catch (Exception e)
            {
                var copia = archivo + ".danado-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Copy(archivo, copia, true);
                log.LogError(e, "No se pudo leer {Archivo}. Copia guardada en {Copia}", archivo, copia);
            }
        }
        var nuevo = new Datos { Equipo = equipoInicial, Creado = DateTime.UtcNow.ToString("o") };
        File.WriteAllText(archivo, JsonSerializer.Serialize(nuevo, Json));
        return nuevo;
    }

    public byte[] RespaldoJson() => JsonSerializer.SerializeToUtf8Bytes(Actual, Json);
    static string NuevoId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    static string Ahora() => DateTime.UtcNow.ToString("o");

    /* ---------- Operaciones ---------- */

    public Task<string> CrearTarea(Tarea entrada, string usuario) => Escribir(d =>
    {
        var t = Limpiar(entrada.Clonar(), d);
        t.Id = NuevoId();
        t.Orden = d.Tareas.Where(x => x.Fase == t.Fase).Select(x => x.Orden).DefaultIfEmpty(0).Max() + 1;
        t.Creada = t.Actualizada = Ahora();
        t.CreadaPor = usuario;
        d.Tareas.Add(t);
        return t.Id;
    });

    public Task<string> ActualizarTarea(Tarea entrada, string usuario) => Escribir(d =>
    {
        var i = d.Tareas.FindIndex(x => x.Id == entrada.Id);
        if (i < 0) throw new ErrorValidacion("La tarea ya no existe (quizá la eliminó la otra persona).");
        var t = Limpiar(entrada.Clonar(), d);
        t.Creada = d.Tareas[i].Creada; t.CreadaPor = d.Tareas[i].CreadaPor; t.Orden = d.Tareas[i].Orden;
        t.Actualizada = Ahora(); t.ActualizadaPor = usuario;
        d.Tareas[i] = t;
        return t.Id;
    });

    public Task<string> CambiarEstado(string id, string estado, string usuario) => Escribir(d =>
    {
        var t = d.Tareas.FirstOrDefault(x => x.Id == id) ?? throw new ErrorValidacion("La tarea ya no existe.");
        t.Estado = Catalogo.Estados.ContainsKey(estado) ? estado : "pendiente";
        t.Actualizada = Ahora(); t.ActualizadaPor = usuario;
        return id;
    });

    /// <summary>Marca o desmarca una subtarea desde la lista o el tablero (sin abrir el editor).</summary>
    /// <remarks>Se identifica por posición y texto, por si la otra persona la editó mientras tanto.</remarks>
    public Task<string> MarcarSubtarea(string id, int indice, string texto, bool hecha, string usuario) => Escribir(d =>
    {
        var t = d.Tareas.FirstOrDefault(x => x.Id == id) ?? throw new ErrorValidacion("La tarea ya no existe.");
        var s = indice >= 0 && indice < t.Subtareas.Count && t.Subtareas[indice].Texto == texto
            ? t.Subtareas[indice]
            : t.Subtareas.FirstOrDefault(x => x.Texto == texto) ?? throw new ErrorValidacion("La subtarea cambió. Revisa la tarea.");
        s.Hecha = hecha;
        t.Actualizada = Ahora(); t.ActualizadaPor = usuario;
        return id;
    });

    public Task<string> EliminarTarea(string id) => Escribir(d =>
    {
        if (d.Tareas.RemoveAll(x => x.Id == id) == 0) throw new ErrorValidacion("La tarea ya no existe.");
        return id;
    });

    /// <param name="fases">Fases en el orden deseado (Id vacío = fase nueva).</param>
    /// <param name="renombres">Nombre anterior → nombre nuevo, para reasignar las tareas.</param>
    public Task<string> GuardarConfig(List<Fase> fases, List<string> equipo, Dictionary<string, string> renombres) => Escribir(d =>
    {
        var nuevas = fases.Where(f => !string.IsNullOrWhiteSpace(f.Nombre) || !string.IsNullOrEmpty(f.Id)).Take(30)
            .Select((f, i) => new Fase
            {
                Id = d.Fases.Any(x => x.Id == f.Id) ? f.Id : NuevoId(),
                Nombre = Txt(f.Nombre, 80) is { Length: > 0 } n ? n : $"Fase {i + 1}",
                Orden = i + 1
            }).ToList();
        var ids = nuevas.Select(f => f.Id).ToHashSet();
        if (d.Tareas.Any(t => t.Fase.Length > 0 && !ids.Contains(t.Fase)))
            throw new ErrorValidacion("Hay fases con tareas. Mueve o elimina sus tareas antes de borrar la fase.");
        var eq = equipo.Select(p => Txt(p, 80)).Where(p => p.Length > 0).Distinct().Take(20).ToList();
        foreach (var (ant, nuevo) in renombres)
        {
            var n = Txt(nuevo, 80);
            if (n.Length > 0 && eq.Contains(n))
                foreach (var t in d.Tareas.Where(t => t.Responsable == ant)) t.Responsable = n;
        }
        d.Fases = nuevas;
        d.Equipo = eq;
        return "";
    });

    /* ---------- Internos ---------- */

    static string Txt(string? v, int max) { var s = (v ?? "").Trim(); return s.Length > max ? s[..max] : s; }
    static string Fecha(string? v) => DateOnly.TryParseExact(v ?? "", "yyyy-MM-dd", out _) ? v! : "";

    static Tarea Limpiar(Tarea t, Datos d)
    {
        t.Titulo = Txt(t.Titulo, 200);
        if (t.Titulo.Length == 0) throw new ErrorValidacion("Escribe el nombre de la tarea.");
        t.Fase = d.Fases.Any(f => f.Id == t.Fase) ? t.Fase : "";
        t.Responsable = Txt(t.Responsable, 80);
        t.Inicio = Fecha(t.Inicio);
        t.Fin = Fecha(t.Fin);
        if (t.Inicio.Length > 0 && t.Fin.Length > 0 && string.CompareOrdinal(t.Fin, t.Inicio) < 0)
            throw new ErrorValidacion("La entrega no puede ser antes del inicio.");
        t.Estado = Catalogo.Estados.ContainsKey(t.Estado) ? t.Estado : "pendiente";
        t.Prioridad = Catalogo.Prioridades.ContainsKey(t.Prioridad) ? t.Prioridad : "media";
        t.Entregable = Txt(t.Entregable, 300);
        t.Notas = Txt(t.Notas, 5000);
        t.Subtareas = (t.Subtareas ?? []).Select(s => new Subtarea { Texto = Txt(s.Texto, 300), Hecha = s.Hecha })
            .Where(s => s.Texto.Length > 0).Take(100).ToList();
        return t;
    }

    async Task<string> Escribir(Func<Datos, string> fn)
    {
        string r;
        await candado.WaitAsync();
        try
        {
            // Se trabaja sobre una copia: si algo falla, lo publicado queda intacto
            var copia = JsonSerializer.Deserialize<Datos>(JsonSerializer.SerializeToUtf8Bytes(Actual, Json), Json)!;
            r = fn(copia);
            copia.Rev++;
            copia.Actualizado = Ahora();
            await Guardar(copia);
            Actual = copia;
        }
        finally { candado.Release(); }
        try { Cambio?.Invoke(); } catch (Exception e) { log.LogWarning(e, "Error notificando el cambio"); }
        return r;
    }

    async Task Guardar(Datos d)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(d, Json);
        var tmp = archivo + ".tmp";
        await File.WriteAllBytesAsync(tmp, bytes);
        File.Move(tmp, archivo, overwrite: true);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(respaldos, $"cronograma-{Fechas.Hoy:yyyy-MM-dd}.json"), bytes);
            foreach (var viejo in Directory.GetFiles(respaldos, "cronograma-*.json").Order().SkipLast(30)) File.Delete(viejo);
        }
        catch (Exception e) { log.LogWarning(e, "No se pudo escribir el respaldo diario"); }
    }
}

public sealed class ErrorValidacion(string mensaje) : Exception(mensaje);
