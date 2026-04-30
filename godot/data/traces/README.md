# Traces

Ce dossier contient maintenant trois types de fichiers :

- `sample_mame_enemy_trace.jsonl` : mini trace artificielle fournie avec le labo.
- `ladybug_enemy_trace_movement_only_000_624.jsonl` : trace MAME réelle limitée au mouvement pur enemy0.
- `ladybug_enemy_trace_movement_plus_collision_000_625.jsonl` : même trace avec la frame de collision incluse.
- `ladybug_enemy_trace_initial_snapshot.json` : snapshot initial complet de la RAM/VRAM/portes.

Dans l'interface Godot, utilise d'abord :

```text
res://data/traces/ladybug_enemy_trace_movement_only_000_624.jsonl
```

Puis clique sur :

1. `Charger` à côté de Trace MAME
2. `Analyser décisions MAME`
3. `Générer candidate pixel+centres`
4. `Comparer mouvement enemy0`

La candidate pixel+centres doit diverger : c'est attendu. La première divergence indique le premier cas de validation/fallback/porte à implémenter.
