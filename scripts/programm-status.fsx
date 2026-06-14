// Aggregiert die Konvergenz aller Säulen zu einem Programm-Dashboard (STATUS.md).
// Aufruf: dotnet fsi scripts/programm-status.fsx [basis-verzeichnis]
// Erfüllt spec-programm-dashboard und spec-drei-saeulen.

open System
open System.IO
open System.Text.Json

let basis = let a = fsi.CommandLineArgs |> Array.tail in if a.Length > 0 then a.[0] else ".."

// Die Säulen des Programms (Reihenfolge = Abhängigkeit).
let saeulen =
    [ "Werkzeug (CDD)",       "cong-driven-development"
      "Anwendung (Runenruf)", "runenruf"
      "Beleg (Fallstudie)",   "cdd-fallstudie"
      "Programm",             "cdd-programm" ]

let private convOf (f: string) =
    use doc = JsonDocument.Parse(File.ReadAllText f)
    doc.RootElement.GetProperty("Convergence").GetString()

let messung repo =
    let d = Path.Combine(basis, repo, ".spot")
    if not (Directory.Exists d) then None
    else
        let fs = Directory.GetFiles(d, "*.json")
        let aligned = fs |> Array.filter (fun f -> convOf f = "Aligned") |> Array.length
        Some {| Knoten = fs.Length; Aligned = aligned |}

let zeilen = ResizeArray<string>()
zeilen.Add "# Programm-Status"
zeilen.Add ""
zeilen.Add (sprintf "_Generiert am %s — aggregierte Konvergenz über alle Säulen (nicht von Hand)._" (DateTime.UtcNow.ToString "yyyy-MM-dd"))
zeilen.Add ""
zeilen.Add "| Säule | Repo | Knoten | Aligned | Quote |"
zeilen.Add "|---|---|---:|---:|---:|"

let mutable summeKnoten = 0
let mutable summeAligned = 0
let mutable fehlend = []
for (name, repo) in saeulen do
    match messung repo with
    | Some m ->
        summeKnoten <- summeKnoten + m.Knoten
        summeAligned <- summeAligned + m.Aligned
        let quote = if m.Knoten = 0 then 0.0 else 100.0 * float m.Aligned / float m.Knoten
        zeilen.Add (sprintf "| %s | `%s` | %d | %d | %.0f %% |" name repo m.Knoten m.Aligned quote)
    | None ->
        fehlend <- repo :: fehlend
        zeilen.Add (sprintf "| %s | `%s` | — | — | fehlt |" name repo)

let gesamt = if summeKnoten = 0 then 0.0 else 100.0 * float summeAligned / float summeKnoten
zeilen.Add (sprintf "| **Programm gesamt** | | **%d** | **%d** | **%.0f %%** |" summeKnoten summeAligned gesamt)
zeilen.Add ""
if not (List.isEmpty fehlend) then
    zeilen.Add (sprintf "> ⚠️ Fehlende Säulen: %s" (String.Join(", ", fehlend)))
zeilen.Add ""
zeilen.Add "Erhebung: `dotnet fsi scripts/programm-status.fsx`"

File.WriteAllText("STATUS.md", String.Join("\n", zeilen) + "\n")
printfn "STATUS.md geschrieben — Programm gesamt %d/%d Knoten Aligned (%.0f %%)" summeAligned summeKnoten gesamt
