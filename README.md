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

## Stand
Programm-SPOT (Vision, Säulen, Roadmap, Risiken) ist modelliert; das Dashboard misst live.
Mapper/Runner, Chat-Steuerung und Anbieter-Naht sind als **Pending**-Specs spezifiziert —
ehrlich der nächste Bau. Lizenz: MPL-2.0.
