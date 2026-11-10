namespace Cronograma.Dominio;

public sealed class Datos
{
    public int Rev { get; set; } = 1;
    public List<Fase> Fases { get; set; } = [];
    public List<Tarea> Tareas { get; set; } = [];
    public List<string> Equipo { get; set; } = [];
    public string? Creado { get; set; }
    public string? Actualizado { get; set; }
}

public sealed class Fase
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Orden { get; set; }
}

public sealed class Subtarea
{
    public string Texto { get; set; } = "";
    public bool Hecha { get; set; }
}

public sealed class Tarea
{
    public string Id { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Fase { get; set; } = "";
    public string Responsable { get; set; } = "";
    public string Inicio { get; set; } = "";   // yyyy-MM-dd
    public string Fin { get; set; } = "";      // yyyy-MM-dd (entrega)
    public string Estado { get; set; } = "pendiente";
    public string Prioridad { get; set; } = "media";
    public string Entregable { get; set; } = "";
    public string Notas { get; set; } = "";
    public double Orden { get; set; }
    public List<Subtarea> Subtareas { get; set; } = [];
    public string? Creada { get; set; }
    public string? Actualizada { get; set; }
    public string? CreadaPor { get; set; }
    public string? ActualizadaPor { get; set; }

    public Tarea Clonar()
    {
        var t = (Tarea)MemberwiseClone();
        t.Subtareas = (Subtareas ?? []).Select(s => new Subtarea { Texto = s.Texto, Hecha = s.Hecha }).ToList();
        return t;
    }

    /// <summary>Avance 0–1: hecha = 100 %, si no, según subtareas (máx. 95 %); en curso sin subtareas = 30 %.</summary>
    public double Progreso => Estado == "hecha" ? 1
        : Subtareas.Count == 0 ? (Estado == "en_curso" ? .3 : 0)
        : Math.Min(.95, (double)Subtareas.Count(s => s.Hecha) / Subtareas.Count);

    /// <summary>"vencida", "pronto" (≤ 2 días) o "".</summary>
    public string Semaforo
    {
        get
        {
            if (Estado == "hecha" || Fechas.Parse(Fin) is not { } f) return "";
            var d = Fechas.Dias(Fechas.Hoy, f);
            return d < 0 ? "vencida" : d <= 2 ? "pronto" : "";
        }
    }
}

public static class Catalogo
{
    public static readonly Dictionary<string, string> Estados = new()
    {
        ["pendiente"] = "Pendiente", ["en_curso"] = "En curso", ["bloqueada"] = "Bloqueada", ["hecha"] = "Hecha"
    };
    public static readonly Dictionary<string, string> Prioridades = new()
    {
        ["alta"] = "Alta", ["media"] = "Media", ["baja"] = "Baja"
    };
    /// <summary>Colores de fase tomados del manual de marca (principal, secundarios y neutros).</summary>
    public static readonly string[] ColoresFase = ["#0076e3", "#00d4a1", "#00c9ff", "#c3cfdb", "#4f6d8f", "#8fb8de"];
}

/// <summary>Fechas en hora de Colombia (UTC−5, sin horario de verano) y festivos (Ley Emiliani).</summary>
public static class Fechas
{
    public static readonly string[] Meses = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];
    static readonly string[] Mes3 = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];
    public static readonly string[] Dias3 = ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"];

    public static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
    public static DateOnly? Parse(string? s) => DateOnly.TryParseExact(s ?? "", "yyyy-MM-dd", out var d) ? d : null;
    public static string Ymd(DateOnly d) => d.ToString("yyyy-MM-dd");
    public static int Dias(DateOnly a, DateOnly b) => b.DayNumber - a.DayNumber;
    public static string Corto(string? s) => Parse(s) is { } d ? Corto(d) : "—";
    public static string Corto(DateOnly d) => $"{Dias3[(int)d.DayOfWeek]} {d.Day} {Mes3[d.Month - 1]}";
    public static DateOnly Lunes(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    public static bool FinDeSemana(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    static readonly Dictionary<int, Dictionary<DateOnly, string>> cache = [];
    public static string? Festivo(DateOnly d)
    {
        Dictionary<DateOnly, string> f;
        lock (cache) if (!cache.TryGetValue(d.Year, out f!)) cache[d.Year] = f = FestivosDe(d.Year);
        return f.GetValueOrDefault(d);
    }

    static Dictionary<DateOnly, string> FestivosDe(int y)
    {
        // Domingo de Pascua (algoritmo de Meeus/Jones/Butcher)
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3,
            h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451,
            mes = (h + l - 7 * m + 114) / 31, dia = (h + l - 7 * m + 114) % 31 + 1;
        var pascua = new DateOnly(y, mes, dia);
        static DateOnly Lun(DateOnly x) { while (x.DayOfWeek != DayOfWeek.Monday) x = x.AddDays(1); return x; }
        var r = new Dictionary<DateOnly, string>();
        void Put(DateOnly x, string n) => r.TryAdd(x, n);
        Put(new(y, 1, 1), "Año Nuevo"); Put(new(y, 5, 1), "Día del Trabajo"); Put(new(y, 7, 20), "Independencia");
        Put(new(y, 8, 7), "Batalla de Boyacá"); Put(new(y, 12, 8), "Inmaculada Concepción"); Put(new(y, 12, 25), "Navidad");
        Put(Lun(new(y, 1, 6)), "Reyes Magos"); Put(Lun(new(y, 3, 19)), "San José"); Put(Lun(new(y, 6, 29)), "San Pedro y San Pablo");
        Put(Lun(new(y, 8, 15)), "Asunción"); Put(Lun(new(y, 10, 12)), "Día de la Raza"); Put(Lun(new(y, 11, 1)), "Todos los Santos");
        Put(Lun(new(y, 11, 11)), "Indep. de Cartagena");
        Put(pascua.AddDays(-3), "Jueves Santo"); Put(pascua.AddDays(-2), "Viernes Santo");
        Put(pascua.AddDays(43), "Ascensión"); Put(pascua.AddDays(64), "Corpus Christi"); Put(pascua.AddDays(71), "Sagrado Corazón");
        return r;
    }
}
