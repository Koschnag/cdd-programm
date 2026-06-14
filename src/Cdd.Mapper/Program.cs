using System.Diagnostics;
using Cdd.Mapper;

// Der Mapper: CDD treibt Claude Code (claude -p) und loopt bis Konvergenz (adr-004, spec-mapper-runner).
//   cdd-mapper --root <projekt> [--max-attempts 3] [--max-specs 1] [--go]
// Ohne --go: Dry-Run — zeigt nur den Plan und die Prompts, ruft NICHTS auf, kostet nichts.

string Arg(string name, string fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

var root = Path.GetFullPath(Arg("--root", "."));
var maxAttempts = int.TryParse(Arg("--max-attempts", "3"), out var ma) ? ma : 3;
var maxSpecs = int.TryParse(Arg("--max-specs", "1"), out var ms) ? ms : 1;
var go = args.Contains("--go");
var projektName = new DirectoryInfo(root).Name;

var nurSpec = Arg("--spec", "");
var specs = MapperCore.FindePendingSpecs(root);
if (nurSpec != "") specs = specs.Where(s => s.Id == nurSpec).ToList();
if (specs.Count == 0)
{
    Console.WriteLine(nurSpec != ""
        ? $"Spec '{nurSpec}' ist in {projektName} nicht Pending (oder existiert nicht)."
        : $"Keine Pending-Specs in {projektName} — nichts zu tun (Modell konvergiert).");
    return 0;
}

Console.WriteLine($"Mapper · Projekt '{projektName}' · {specs.Count} Pending-Spec(s) · "
                  + $"max {maxSpecs} diesen Lauf, max {maxAttempts} Versuche je Spec · "
                  + (go ? "MODUS: GO (autonom)" : "MODUS: Dry-Run (nichts wird aufgerufen)"));

var ziele = specs.Take(maxSpecs).ToList();
var ergebnisse = new List<RunResult>();

foreach (var spec in ziele)
{
    var prompt = MapperCore.BuildPrompt(spec, projektName);
    Console.WriteLine($"\n── {spec.Id}: {spec.Title} ──");

    if (!go)
    {
        Console.WriteLine("[Dry-Run] Prompt, der an `claude -p` ginge:\n");
        Console.WriteLine(prompt);
        Console.WriteLine("[Dry-Run] Danach: cdd validate + cdd sync-tests --write, Loop bis Aligned.");
        continue;
    }

    var ergebnis = MapperCore.RunSpec(spec, maxAttempts, attempt =>
    {
        Console.WriteLine($"  Versuch {attempt}/{maxAttempts} → claude -p …");
        if (!Lauf("claude", new[] { "-p", prompt, "--permission-mode", "acceptEdits" }, root))
            Console.WriteLine("  (claude-Aufruf meldete Fehler — messe trotzdem das Gate)");
        Lauf("cdd", new[] { "validate" }, root);
        Lauf("cdd", new[] { "sync-tests", "--write" }, root);
        var ok = MapperCore.SpecKonvergiert(root, spec.Id);
        Console.WriteLine(ok ? "  ✓ Spec konvergiert (Tests Aligned)" : "  … noch nicht konvergiert");
        return ok;
    });
    ergebnisse.Add(ergebnis);
    Console.WriteLine(ergebnis.Konvergiert
        ? $"  ABGESCHLOSSEN nach {ergebnis.Versuche} Versuch(en)."
        : $"  AUFGEGEBEN nach {ergebnis.Versuche} Versuchen — bitte manuell ansehen.");
}

if (go)
{
    var ok = ergebnisse.Count(r => r.Konvergiert);
    Console.WriteLine($"\nFertig: {ok}/{ergebnisse.Count} Spec(s) konvergiert.");
    return ergebnisse.All(r => r.Konvergiert) ? 0 : 1;
}
return 0;

// Startet einen Prozess im Projektverzeichnis; true bei Exit-Code 0.
static bool Lauf(string datei, string[] argumente, string cwd)
{
    try
    {
        var psi = new ProcessStartInfo(datei) { WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var a in argumente) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p is null) return false;
        p.WaitForExit();
        return p.ExitCode == 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Fehler beim Start von {datei}: {ex.Message}");
        return false;
    }
}
