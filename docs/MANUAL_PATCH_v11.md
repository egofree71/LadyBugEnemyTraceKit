# Manual patch v11

Copier les fichiers du zip dans le dépôt `LadyBugEnemyTraceKit` en conservant les chemins.

## Remplacer

- `godot/scripts/Main.cs`

## Ajouter

- `godot/scripts/TraceLab/Compare/ForcedReversalAnalyzer.cs`
- `godot/scripts/TraceLab/Sim/LearnedReversalProbeCandidateTraceFactory.cs`

## Conserver / ajouter si absent

- `godot/scripts/TraceLab/Sim/CorridorGatedCandidateTraceFactory.cs`

## Test rapide dans Godot

1. Lancer la scène principale.
2. Mettre `res://data/traces/ladybug_enemy_trace_trace.jsonl` dans le champ Trace MAME.
3. Cliquer `Charger`.
4. Cliquer `Analyser demi-tours MAME`.
5. Cliquer `Générer candidate reversal-probe v11`.
6. Cliquer `Comparer mouvement enemy0`.

Le but n'est pas forcément d'avoir zéro différence immédiatement, mais de voir si la première divergence recule par rapport à la v10, qui divergeait au sample 39 sur cette trace.
