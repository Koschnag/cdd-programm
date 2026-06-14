module Programm.Tests

open System.IO
open System.Text.RegularExpressions
open Xunit

let rec private wurzel dir =
    if File.Exists(Path.Combine(dir, "STATUS.md")) then dir
    else
        let e = Directory.GetParent dir
        if isNull e then failwith "STATUS.md nicht gefunden — Dashboard zuerst generieren" else wurzel e.FullName

let private status = lazy File.ReadAllText(Path.Combine(wurzel (Directory.GetCurrentDirectory()), "STATUS.md"))

[<Fact; Trait("spot", "spec-drei-saeulen-test-1")>]
let ``Alle drei Saeulen erscheinen im Status`` () =
    let s = status.Value
    for repo in [ "cong-driven-development"; "runenruf"; "cdd-fallstudie" ] do
        Assert.True(s.Contains repo, sprintf "Säule %s fehlt im Programm-Status" repo)

[<Fact; Trait("spot", "spec-drei-saeulen-test-2")>]
let ``Keine Saeule wird als fehlend gemeldet`` () =
    Assert.DoesNotContain("fehlt", status.Value)

[<Fact; Trait("spot", "spec-programm-dashboard-test-1")>]
let ``Dashboard nennt Knoten, Aligned und eine Gesamtquote`` () =
    let s = status.Value
    Assert.Contains("Aligned", s)
    Assert.Contains("Programm gesamt", s)

[<Fact; Trait("spot", "spec-programm-dashboard-test-2")>]
let ``Gesamtquote ist eine plausible Prozentzahl`` () =
    let m = Regex.Match(status.Value, @"Programm gesamt.*?\*\*(\d+) %\*\*")
    Assert.True(m.Success, "Keine Programm-Gesamtquote gefunden")
    let quote = int m.Groups.[1].Value
    Assert.InRange(quote, 1, 100)
