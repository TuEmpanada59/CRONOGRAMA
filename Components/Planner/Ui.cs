using Cronograma.Dominio;

namespace Cronograma.Components.Planner;

/// <summary>Ayudas de presentación compartidas por las vistas.</summary>
public static class Ui
{
    public sealed record Grupo(string Id, string Nombre);

    /// <summary>Marcar/desmarcar una subtarea sin abrir el editor.</summary>
    public sealed record CambioSubtarea(string TareaId, int Indice, string Texto, bool Hecha);

    /// <summary>Fases en orden + un grupo "Sin fase" si hay tareas huérfanas.</summary>
    public static List<Grupo> Grupos(Datos d)
    {
        var g = d.Fases.OrderBy(f => f.Orden).Select(f => new Grupo(f.Id, f.Nombre)).ToList();
        if (d.Tareas.Any(t => SinFase(d, t))) g.Add(new Grupo("", "Sin fase"));
        return g;
    }

    static bool SinFase(Datos d, Tarea t) => t.Fase.Length == 0 || !d.Fases.Any(f => f.Id == t.Fase);
    public static bool EnFase(Datos d, Tarea t, string id) => id.Length > 0 ? t.Fase == id : SinFase(d, t);
    public static int FaseIdx(Datos d, string id) => Math.Max(0, d.Fases.FindIndex(f => f.Id == id));
    public static string Color(Datos d, string id) => Catalogo.ColoresFase[FaseIdx(d, id) % Catalogo.ColoresFase.Length];
    public static string NombreFase(Datos d, string id) => d.Fases.FirstOrDefault(f => f.Id == id)?.Nombre ?? "Sin fase";

    public static IOrderedEnumerable<Tarea> Ordenar(IEnumerable<Tarea> ts) =>
        ts.OrderBy(t => t.Orden).ThenBy(t => t.Inicio, StringComparer.Ordinal);

    public static double Avance(IEnumerable<Tarea> ts)
    {
        var l = ts.ToList();
        return l.Count == 0 ? 0 : l.Average(t => t.Progreso);
    }

    /// <summary>Filtros de la barra de herramientas (se usan también en el informe PDF).</summary>
    public static List<Tarea> Filtrar(Datos d, string? resp, string? estado, string? texto)
    {
        resp ??= ""; estado ??= "";
        var q = (texto ?? "").Trim().ToLowerInvariant();
        return d.Tareas.Where(t =>
            (resp.Length == 0 || (resp == "__none" ? t.Responsable.Length == 0 : t.Responsable == resp)) &&
            (estado.Length == 0 || (estado == "abiertas" ? t.Estado != "hecha" : t.Estado == estado)) &&
            (q.Length == 0 || t.Titulo.ToLowerInvariant().Contains(q) || t.Notas.ToLowerInvariant().Contains(q) ||
             t.Subtareas.Any(s => s.Texto.ToLowerInvariant().Contains(q)))).ToList();
    }

    /// <summary>Texto legible de los filtros activos, o null si no hay.</summary>
    public static string? DescribirFiltros(string? resp, string? estado, string? texto)
    {
        var partes = new List<string>();
        if (!string.IsNullOrEmpty(resp)) partes.Add(resp == "__none" ? "sin responsable" : $"responsable: {resp}");
        if (!string.IsNullOrEmpty(estado)) partes.Add(estado == "abiertas" ? "solo sin terminar" : $"estado: {Catalogo.Estados.GetValueOrDefault(estado, estado)}");
        if (!string.IsNullOrWhiteSpace(texto)) partes.Add($"búsqueda: “{texto.Trim()}”");
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    public static string Pct(double p) => $"{Math.Round(p * 100)}%";
    public static string Num(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string TextoSemaforo(Tarea t)
    {
        var s = t.Semaforo;
        if (s.Length == 0 || Fechas.Parse(t.Fin) is not { } fin) return "";
        var d = Fechas.Dias(Fechas.Hoy, fin);
        return s == "vencida" ? $"Vencida {-d} d" : d == 0 ? "Vence hoy" : d == 1 ? "Vence mañana" : $"Vence en {d} d";
    }
}
