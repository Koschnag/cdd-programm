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
