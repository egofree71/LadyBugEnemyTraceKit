# Manual patch v13

Copier les fichiers du zip dans le dépôt `LadyBugEnemyTraceKit` en conservant les chemins.

## Remplacer

- `godot/scripts/Main.cs`

## Ajouter

- `godot/scripts/TraceLab/Compare/PreferenceTimingProbeAnalyzer.cs`
- `godot/scripts/TraceLab/Sim/TimingProbeCandidateTraceFactory.cs`

## Déjà inclus depuis v11/v12 pour garder l'overlay complet

- `godot/scripts/TraceLab/Compare/ForcedReversalAnalyzer.cs`
- `godot/scripts/TraceLab/Sim/CorridorGatedCandidateTraceFactory.cs`
- `godot/scripts/TraceLab/Sim/LearnedReversalProbeCandidateTraceFactory.cs`

## Test rapide dans Godot

1. Lancer la scène principale.
2. Vérifier que `Trace MAME` vaut `res://data/traces/ladybug_enemy_trace_trace.jsonl`.
3. Cliquer `Charger`.
4. Cliquer `Analyser timing v13`.
5. Cliquer `Générer candidate timing-probe v13`.
6. Cliquer `Comparer mouvement enemy0`.

Si la candidate timing-probe arrive à zéro différence sur cette trace, cela ne prouve pas encore l'IA finale. Cela prouve surtout que les divergences restantes sont explicables par l'état de décision au centre, pas par le mouvement pixel.
