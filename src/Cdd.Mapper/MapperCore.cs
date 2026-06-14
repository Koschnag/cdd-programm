using System.Text;
using System.Text.Json;

namespace Cdd.Mapper;

/// <summary>Eine noch nicht umgesetzte Spezifikation aus dem SPOT.</summary>
public sealed record PendingSpec(string Id, string Title, string Intent, string[] Kriterien);

/// <summary>Ergebnis eines Spec-Laufs.</summary>
public sealed record RunResult(string SpecId, bool Konvergiert, int Versuche);

/// <summary>
/// Reine Logik des Mappers — ohne IO testbar (spec-mapper-runner).
/// Der Mapper übersetzt Pending-Specs in Prompts für einen Ausführer (claude -p),
/// lässt das Konvergenz-Gate messen und loopt bis grün oder bis zum Versuchslimit.
/// </summary>
public static class MapperCore
{
    /// <summary>Liest .spot und liefert alle Pending-SpecNodes (ohne abgeleitete TestNodes).</summary>
    public static List<PendingSpec> FindePendingSpecs(string root)
    {
        var dir = Path.Combine(root, ".spot");
        var result = new List<PendingSpec>();
        if (!Directory.Exists(dir)) return result;
        foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(x => x))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            var r = doc.RootElement;
            if (Conv(r) != "Pending") continue;
            var payload = r.GetProperty("Payload");
            if (payload.GetProperty("Case").GetString() != "SpecNode") continue;
            var id = r.GetProperty("Id").GetString() ?? Path.GetFileNameWithoutExtension(f);
            if (id.Contains("-test-")) continue;
            var item = payload.GetProperty("Fields").GetProperty("Item");
            var krit = new List<string>();
            if (item.TryGetProperty("Criteria", out var cs))
                foreach (var c in cs.EnumerateArray())
                    krit.Add($"GEGEBEN {Str(c, "Given")} WENN {Str(c, "When")} DANN {Str(c, "Then")}");
            result.Add(new PendingSpec(id, Str(item, "Title"), Str(item, "Intent"), krit.ToArray()));
        }
        return result;
    }

    /// <summary>Baut den Prosa-Prompt, mit dem der Ausführer die Spec gegen das Gate umsetzt.</summary>
    public static string BuildPrompt(PendingSpec s, string projektName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Du arbeitest im Projekt '{projektName}' (spec-driven mit CDD).");
        sb.AppendLine($"Setze die folgende Spezifikation um, bis das Konvergenz-Gate grün ist.");
        sb.AppendLine();
        sb.AppendLine($"SPEC {s.Id}: {s.Title}");
        sb.AppendLine($"Absicht: {s.Intent}");
        sb.AppendLine("Akzeptanzkriterien:");
        foreach (var k in s.Kriterien) sb.AppendLine($"  - {k}");
        sb.AppendLine();
        sb.AppendLine("Vorgehen: F# für Domäne/Modelle, C# für Technik. Implementiere die Tests");
        sb.AppendLine("zu dieser Spec und den Produktivcode, dann führe `cdd validate` und");
        sb.AppendLine("`cdd sync-tests --write` aus, bis die Test-Knoten dieser Spec Aligned sind.");
        sb.AppendLine("WICHTIG: Verändere niemals die Spec oder die Tests, nur um das Gate zu täuschen,");
        sb.AppendLine("und setze den Convergence-Status nicht von Hand. Das Orakel muss ehrlich bleiben.");
        return sb.ToString();
    }

    /// <summary>
    /// Loop-Steuerung pro Spec (rein, testbar): ruft den Ausführer bis zu maxVersuche-mal,
    /// bricht ab, sobald er Konvergenz meldet — sonst Aufgabe-Meldung (spec-mapper-runner Kriterium 2).
    /// </summary>
    public static RunResult RunSpec(PendingSpec spec, int maxVersuche, Func<int, bool> versuch)
    {
        for (var a = 1; a <= maxVersuche; a++)
            if (versuch(a)) return new RunResult(spec.Id, true, a);
        return new RunResult(spec.Id, false, maxVersuche);
    }

    /// <summary>Misst, ob alle Test-Knoten einer Spec im .spot Aligned sind.</summary>
    public static bool SpecKonvergiert(string root, string specId)
    {
        var dir = Path.Combine(root, ".spot");
        var tests = Directory.GetFiles(dir, $"{specId}-test-*.json");
        if (tests.Length == 0) return false;
        foreach (var f in tests)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            if (Conv(doc.RootElement) != "Aligned") return false;
        }
        return true;
    }

    private static string Conv(JsonElement e) =>
        e.TryGetProperty("Convergence", out var v) ? v.GetString() ?? "Pending" : "Pending";

    private static string Str(JsonElement e, string n) =>
        e.TryGetProperty(n, out var v) ? v.GetString() ?? "" : "";
}
