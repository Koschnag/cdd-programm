# Regeln für KI-Agenten in diesem Repo

1. **Programm-Ebene:** `.spot/` modelliert das Gesamtvorhaben über die drei Projekt-Repos,
   nicht ein einzelnes Projekt. Säulen sind Component-Knoten mit DependsOn.
2. **Dashboard ist generiert:** `STATUS.md` kommt aus `scripts/programm-status.fsx` über die
   echten `.spot/`-Stände — nie von Hand pflegen.
3. **Ehrlicher Status:** Mapper/Runner, Chat-Steuerung und Anbieter-Naht sind Pending, bis
   gebaut und gegen ein Gate belegt. Nicht vorzeitig auf Aligned setzen.
4. **Der Runner (wenn gebaut) loopt bis Konvergenz**, aber nur durch das Konvergenz-Gate und
   per PR — Versuchs-/Token-Limit je Spec ist Pflicht (risk-runner-amok).
5. **Kein Python im Repo.** Skripte und Tests sind F#/.NET.
6. **Release-Tags (`v*`) nur nach explizitem Auftrag des Maintainers.**
