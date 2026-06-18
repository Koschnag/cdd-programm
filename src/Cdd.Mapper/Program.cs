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
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Fehler: --root '{root}' existiert nicht.");
    return 2;
}
var maxAttempts = int.TryParse(Arg("--max-attempts", "3"), out var ma) ? ma : 3;
var maxSpecs = int.TryParse(Arg("--max-specs", "1"), out var ms) ? ms : 1;
var go = args.Contains("--go");
var json = args.Contains("--json");
// --json: eine JSON-Zeile je Ereignis (für die Cockpit-Steuerzentrale, die den Loop wie einen Engine-Turn zeigt).
void Emit(object o) { if (json) Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(o)); }
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
    Emit(new { t = "spec", id = spec.Id, title = spec.Title });

    if (!go)
    {
        Console.WriteLine("[Dry-Run] Prompt, der an `claude -p` ginge:\n");
        Console.WriteLine(prompt);
        Console.WriteLine("[Dry-Run] Danach: cdd validate + cdd sync-tests --write, Loop bis Aligned.");
        continue;
    }

    // Misst das Gate v2: cdd validate + sync-tests, dann echtes `dotnet test` je Projekt.
    // Exit 0 reicht nicht — die Ausgabe muss bestandene Tests ausweisen (sonst gilt "keine Tests" fälschlich als grün).
    (bool ok, bool marker, int proj, bool gruen) GateMessen()
    {
        Lauf("cdd", new[] { "validate" }, root);
        Lauf("cdd", new[] { "sync-tests", "--write" }, root);
        var marker = MapperCore.SpecKonvergiert(root, spec.Id);
        var projekte = MapperCore.FindeTestprojekte(root);
        var alleGruen = projekte.Count > 0;
        foreach (var p in projekte)
        {
            var (exit, ausgabe) = LaufMitAusgabe("dotnet", new[] { "test", p, "--nologo" }, root);
            if (!MapperCore.TestlaufGruen(exit, ausgabe))
            {
                alleGruen = false;
                Console.WriteLine($"  ✗ Tests nicht grün in {Path.GetFileName(p)}");
            }
        }
        return (MapperCore.GateBestanden(marker, projekte.Count, alleGruen), marker, projekte.Count, alleGruen);
    }

    var ergebnis = MapperCore.RunSpec(spec, maxAttempts, attempt =>
    {
        // Idempotent + token-sparsam: eine bereits konvergierte Spec wird NICHT neu gebaut.
        // (Antwort auf die Token-Ökonomie: der Loop verbrennt nichts für schon-grüne Specs.)
        if (attempt == 1 && GateMessen().ok)
        {
            Console.WriteLine("  ✓ Gate bereits grün — kein claude-Lauf nötig (Token gespart).");
            Emit(new { t = "gate", spec = spec.Id, ok = true, skipped = true });
            MapperCore.SetzeSpecAligned(root, spec.Id);   // Orakel verbürgt: Spec → Aligned
            return true;
        }
        Console.WriteLine($"  Versuch {attempt}/{maxAttempts} → claude -p …");
        Emit(new { t = "attempt", n = attempt, max = maxAttempts, spec = spec.Id });
        if (!Lauf("claude", new[] { "-p", prompt, "--permission-mode", "acceptEdits" }, root))
            Console.WriteLine("  (claude-Aufruf meldete Fehler — messe trotzdem das Gate)");
        var (ok, marker, proj, gruen) = GateMessen();
        Emit(new { t = "gate", spec = spec.Id, marker, testprojekte = proj, alleGruen = gruen, ok });
        Console.WriteLine(ok ? "  ✓ Spec konvergiert (Marker Aligned UND dotnet test wirklich grün)"
                             : $"  … noch nicht konvergiert (Marker={marker}, Testprojekte={proj}, alle grün={gruen})");
        if (ok) MapperCore.SetzeSpecAligned(root, spec.Id);   // Orakel verbürgt: Spec → Aligned
        return ok;
    });
    ergebnisse.Add(ergebnis);
    Emit(new { t = "spec_done", id = spec.Id, konvergiert = ergebnis.Konvergiert, versuche = ergebnis.Versuche });
    Console.WriteLine(ergebnis.Konvergiert
        ? $"  ABGESCHLOSSEN nach {ergebnis.Versuche} Versuch(en)."
        : $"  AUFGEGEBEN nach {ergebnis.Versuche} Versuchen — bitte manuell ansehen.");
}

if (go)
{
    var ok = ergebnisse.Count(r => r.Konvergiert);
    Console.WriteLine($"\nFertig: {ok}/{ergebnisse.Count} Spec(s) konvergiert.");
    Emit(new { t = "done", konvergiert = ok, total = ergebnisse.Count });
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

// Startet einen Prozess und fängt die Ausgabe ein (für die Test-Ergebnis-Auswertung).
static (int exit, string ausgabe) LaufMitAusgabe(string datei, string[] argumente, string cwd)
{
    try
    {
        var psi = new ProcessStartInfo(datei)
        {
            WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var a in argumente) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p is null) return (-1, "");
        var o = p.StandardOutput.ReadToEnd();
        var e = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, o + e);
    }
    catch (Exception ex)
    {
        return (-1, ex.Message);
    }
}
