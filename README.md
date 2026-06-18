# CDD-Programm — die Steuerzentrale

Das übergreifende Modell des Gesamtvorhabens: **CDD als Control Plane** über drei Säulen.
Hier wird das Programm geplant, überwacht und (künftig) gesteuert — der Mensch schreibt
Prosa und Specs, die KI führt aus.

## Die drei Säulen
| Säule | Rolle | Repo |
|---|---|---|
| Werkzeug | die IDE/Methode | [cong-driven-development](https://github.com/Koschnag/cong-driven-development) |
| Anwendung | das abgeleitete Produkt | [runenruf](https://github.com/Koschnag/runenruf) |
| Beleg | die wissenschaftliche Fallstudie | [cdd-fallstudie](https://github.com/Koschnag/cdd-fallstudie) |

**Live-Überblick:** [STATUS.md](STATUS.md) — aggregierte Konvergenz über alle Säulen,
generiert aus den echten `.spot/`-Ständen (`dotnet fsi scripts/programm-status.fsx`).

## Die Vision: ein Ort, ein Modus
- **Steuerzentrale:** Du chattest und überwachst nur in CDD; Diagramme zeigen den Stand,
  du passt an. (`premise-control-plane`)
- **Mapper (nächster Bau):** Der Runner übergibt Pending-Specs an Claude Code (`claude -p`,
  headless), lässt das Konvergenz-Gate laufen und spielt den Status zurück —
  **Loop bis Konvergenz** (`adr-004`). Das ist die fehlende Hälfte: heute kann Claude Code
  das Modell lesen/ändern (MCP), künftig treibt das Modell Claude Code.
- **Anbieter-Naht:** Genau am Mapper wird später Opus durch **Mistral** ersetzt — Kostenbeleg
  und europäische Souveränität in einer Konfiguration (`spec-anbieter-naht`).

## Der Mapper (gebaut, v1)

`Cdd.Mapper` ist der Runner, der CDD zur Steuerzentrale macht: er findet Pending-Specs,
baut daraus Prompts, übergibt sie an Claude Code (`claude -p`, headless), lässt das
Konvergenz-Gate messen und loopt bis grün — **Loop bis Konvergenz** (adr-004), mit
Versuchs-/Spec-Limit gegen Amoklauf.

```bash
cdd-mapper --root ../runenruf                 # Dry-Run: zeigt Plan + Prompts (kostenlos)
cdd-mapper --root ../runenruf --go --max-specs 1 --max-attempts 3   # autonom (Tokens!)
```
Standard ist Dry-Run; echter Lauf nur mit `--go`. Hier wird später Claude durch Mistral
ersetzt (Anbieter-Naht, spec-anbieter-naht).

## Stand
Programm-SPOT (Vision, Säulen, Roadmap, Risiken) ist modelliert; das Dashboard misst live.

**Gebaut & belegt:** `Cdd.Mapper` (der Loop-bis-Konvergenz-Runner, `spec-mapper-runner` Aligned,
10/10 Tests, als `dotnet tool` installierbar, idempotent, Orakel verbürgt Spec→Aligned nur bei
echtem grünem Gate) — **end-to-end an Runenruf bewiesen** (`spec-fenster` durch `cdd-mapper --go`,
27/27 Tests grün). Das Cockpit (`cong-driven-development`) treibt den Mapper via `/api/loop/run`.

**Noch Pending (ehrlich der nächste Bau):** Chat-Steuerung im Cockpit, die Anbieter-Naht
Opus→Mistral (`spec-anbieter-naht`), sowie — Manifest-only, nicht im Code — FsCheck-Property-Tests
und Lean-Beweise.

Lizenz: MPL-2.0.
