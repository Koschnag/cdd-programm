
---

## Mapper-Lauf 01 — spec-diplomatie (erster autonomer Lauf)
- **Befehl:** `cdd-mapper --root ../runenruf --go --spec spec-diplomatie --max-attempts 2`
- **Was geschah:** `claude -p` implementierte autonom `Voelker.beziehung`/`sindVerbuendet`
  (`Beziehung = Verbuendet|Verfeindet`) + 2 markierte Tests. Der Mapper maß das Gate
  (`cdd validate` + `cdd sync-tests --write`) → Test-Knoten Aligned → konvergiert nach 1 Versuch.
- **Ehrliche Gegenprobe:** `dotnet test` 24/24 grün — die Tests bestehen wirklich, nicht nur Marker.
- **Ergebnis:** Review-PR (Koschnag/runenruf#5), bewusst ohne Auto-Merge → fork-bar.
- **Protokoll:** `historie/mapper-laeufe/lauf-01-spec-diplomatie.log`.
- **Erkenntnis (für Mapper-Gate v2):** Die KI konnte im nested-Lauf `dotnet test`/`cdd` nicht selbst
  ausführen (Permission-Mode) — sie schrieb nur Code; der **Mapper** maß danach das Gate. Das v1-Gate
  prüft Marker-Existenz + `validate`, nicht das tatsächliche Test-Bestehen. Hier war es grün, aber:
  **Gate v2 muss `dotnet test` einschließen**, sonst könnte ein markierter-aber-roter Test falsch konvergieren.
