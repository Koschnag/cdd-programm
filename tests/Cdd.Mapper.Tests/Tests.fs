module Cdd.Mapper.Tests

open System
open System.IO
open Xunit
open Cdd.Mapper

// Schreibt eine minimale Pending-Spec ins temporäre .spot
let private tempMitSpec () =
    let root = Path.Combine(Path.GetTempPath(), "mapper-test-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, ".spot")) |> ignore
    let json = """{"Id":"spec-demo","Payload":{"Case":"SpecNode","Fields":{"Item":{
        "Title":"Demo-Funktion","Intent":"etwas Nützliches",
        "Criteria":[{"Given":"eine Eingabe X","When":"die Funktion läuft","Then":"kommt Y heraus"}]}}},"Convergence":"Pending"}"""
    File.WriteAllText(Path.Combine(root, ".spot", "spec-demo.json"), json)
    root

[<Fact; Trait("spot", "spec-mapper-runner-test-1")>]
let ``Mapper findet Pending-Spec und baut einen Prompt mit den Kriterien`` () =
    let root = tempMitSpec ()
    try
        let specs = MapperCore.FindePendingSpecs root
        let spec = Assert.Single(specs)
        Assert.Equal("spec-demo", spec.Id)
        let prompt = MapperCore.BuildPrompt(spec, "demo-projekt")
        // Der Prompt trägt Spec-Id, Akzeptanzkriterium und den Gate-Auftrag
        Assert.Contains("spec-demo", prompt)
        Assert.Contains("kommt Y heraus", prompt)
        Assert.Contains("cdd sync-tests", prompt)
        Assert.Contains("nicht von Hand", prompt) // Orakel-Integrität
    finally
        Directory.Delete(root, true)

[<Fact; Trait("spot", "spec-mapper-runner-test-2")>]
let ``Mapper bricht nach dem Versuchslimit ab, statt unbegrenzt zu loopen`` () =
    let spec = PendingSpec("spec-x", "X", "x", [| "GEGEBEN a WENN b DANN c" |])
    // Ausführer, der NIE Konvergenz meldet
    let mutable aufrufe = 0
    let ergebnis = MapperCore.RunSpec(spec, 3, fun _ -> aufrufe <- aufrufe + 1; false)
    Assert.False(ergebnis.Konvergiert)
    Assert.Equal(3, ergebnis.Versuche)
    Assert.Equal(3, aufrufe) // genau maxVersuche, kein endloser Loop

[<Fact>]
let ``Mapper stoppt, sobald ein Versuch Konvergenz meldet`` () =
    let spec = PendingSpec("spec-y", "Y", "y", [||])
    let ergebnis = MapperCore.RunSpec(spec, 5, fun attempt -> attempt = 2) // 2. Versuch klappt
    Assert.True(ergebnis.Konvergiert)
    Assert.Equal(2, ergebnis.Versuche)

[<Fact>]
let ``Aligned-Specs werden nicht als Pending gemeldet`` () =
    let root = Path.Combine(Path.GetTempPath(), "mapper-aligned-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, ".spot")) |> ignore
    File.WriteAllText(Path.Combine(root, ".spot", "spec-ok.json"),
        """{"Id":"spec-ok","Payload":{"Case":"SpecNode","Fields":{"Item":{"Title":"T","Intent":"i","Criteria":[]}}},"Convergence":"Aligned"}""")
    try Assert.Empty(MapperCore.FindePendingSpecs root)
    finally Directory.Delete(root, true)

[<Fact>]
let ``Gate v2: findet Testprojekte unter tests`` () =
    let root = Path.Combine(Path.GetTempPath(), "mapper-tp-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, "tests", "Foo.Tests")) |> ignore
    Directory.CreateDirectory(Path.Combine(root, "tests", "Bar.Tests")) |> ignore
    File.WriteAllText(Path.Combine(root, "tests", "Foo.Tests", "Foo.Tests.fsproj"), "<Project/>")
    File.WriteAllText(Path.Combine(root, "tests", "Bar.Tests", "Bar.Tests.csproj"), "<Project/>")
    try
        let projekte = MapperCore.FindeTestprojekte root
        Assert.Equal(2, projekte.Count)
    finally Directory.Delete(root, true)

[<Fact>]
let ``Gate v2: keine Testprojekte ohne tests-Verzeichnis`` () =
    let root = Path.Combine(Path.GetTempPath(), "mapper-notp-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(root) |> ignore
    try Assert.Empty(MapperCore.FindeTestprojekte root)
    finally Directory.Delete(root, true)

[<Fact>]
let ``Gate v2: keine Testprojekte gilt NIE als konvergiert (Loch geschlossen)`` () =
    Assert.False(MapperCore.GateBestanden(true, 0, true))   // Marker ok, aber 0 Projekte → nicht grün
    Assert.True(MapperCore.GateBestanden(true, 1, true))     // alles erfüllt
    Assert.False(MapperCore.GateBestanden(false, 1, true))   // Marker fehlt
    Assert.False(MapperCore.GateBestanden(true, 1, false))   // Tests rot

[<Fact>]
let ``Gate v2: 'keine Tests gelaufen' (Exit 0) zaehlt NICHT als gruen`` () =
    Assert.False(MapperCore.TestlaufGruen(0, "No test is available in the project."))
    Assert.False(MapperCore.TestlaufGruen(0, "Es sind keine Tests verfügbar."))
    Assert.False(MapperCore.TestlaufGruen(1, "Failed!  - Fehler: 2"))
    Assert.True(MapperCore.TestlaufGruen(0, "Bestanden!  : Fehler: 0, erfolgreich: 5"))
    Assert.True(MapperCore.TestlaufGruen(0, "Passed!  - Failed: 0, Passed: 5"))

[<Fact>]
let ``Orakel verbuergt Spec: Pending -> Aligned, nur das eine Feld`` () =
    let root = Path.Combine(Path.GetTempPath(), "mapper-flip-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, ".spot")) |> ignore
    let pfad = Path.Combine(root, ".spot", "spec-z.json")
    File.WriteAllText(pfad,
        """{"Id":"spec-z","Payload":{"Case":"SpecNode","Fields":{"Item":{"Title":"Titel-bleibt","Intent":"i","Criteria":[]}}},"Convergence":"Pending"}""")
    try
        MapperCore.SetzeSpecAligned(root, "spec-z")
        let txt = File.ReadAllText pfad
        Assert.Contains("\"Convergence\":\"Aligned\"", txt)
        Assert.DoesNotContain("Pending", txt)
        Assert.Contains("Titel-bleibt", txt)   // der Rest unangetastet
    finally Directory.Delete(root, true)

[<Fact>]
let ``Orakel fasst nur Pending an: Diverged bleibt, idempotent`` () =
    let root = Path.Combine(Path.GetTempPath(), "mapper-flip2-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, ".spot")) |> ignore
    let mk id conv =
        let p = Path.Combine(root, ".spot", id + ".json")
        File.WriteAllText(p, sprintf """{"Id":"%s","Payload":{"Case":"SpecNode","Fields":{"Item":{"Title":"T","Intent":"i","Criteria":[]}}},"Convergence":"%s"}""" id conv)
        p
    let dv = mk "spec-dv" "Diverged"
    let al = mk "spec-al" "Aligned"
    try
        MapperCore.SetzeSpecAligned(root, "spec-dv")   // Diverged darf NICHT zu Aligned werden
        Assert.Contains("Diverged", File.ReadAllText dv)
        MapperCore.SetzeSpecAligned(root, "spec-al")   // idempotent: Aligned bleibt Aligned
        Assert.Contains("\"Convergence\":\"Aligned\"", File.ReadAllText al)
    finally Directory.Delete(root, true)
