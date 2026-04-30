# LadyBug Enemy Trace Kit

Kit de départ pour analyser le déplacement des ennemis de Lady Bug avec une trace MAME, puis comparer avec une trace candidate produite côté Godot.

Le kit est volontairement séparé du remake principal. Il sert de labo ponctuel pour comprendre et valider le comportement arcade.

## Contenu

```text
mame/
  ladybug_enemy_trace.lua       Script MAME Lua de capture
  README_MAME.md                Notes de lancement MAME

godot/
  project.godot                 Mini-projet Godot .NET lançable depuis l'éditeur
  scenes/Main.tscn              Scène principale
  scripts/                      Lecteur JSONL, visualiseur, comparateur
  data/traces/                  Dossier où déposer les traces MAME/Godot

docs/
  architecture.md               Architecture et étapes proposées
  trace_schema.md               Format JSON/JSONL utilisé

tools/
  compare_traces.py             Comparateur externe optionnel, utile si Godot ne démarre pas
```

## Étape 1 — Capturer une trace MAME

1. Ouvre `mame/ladybug_enemy_trace.lua`.
2. Renseigne `CONFIG.save_state` si tu veux charger automatiquement un état sauvegardé.
3. Lance MAME avec `-autoboot_script`.
4. Récupère :
   - `ladybug_enemy_trace_initial_snapshot.json`
   - `ladybug_enemy_trace_trace.jsonl`
5. Copie-les dans `godot/data/traces/`.

Voir `mame/README_MAME.md` pour les détails.

## Étape 2 — Ouvrir le mini-projet Godot

1. Ouvre Godot .NET.
2. Importe le dossier `godot/` comme projet.
3. Lance la scène principale depuis l'éditeur.
4. Dans le champ `Trace MAME`, mets par exemple :

```text
res://data/traces/ladybug_enemy_trace_trace.jsonl
```

5. Clique sur `Charger`.

Le panneau de gauche montre le chemin de `enemy0`. Le panneau de droite affiche les logs.

## Étape 3 — Comparer avec une trace candidate

Le projet sait comparer deux JSONL de même format.

Pour vérifier que le pipeline fonctionne :

1. Charge une trace MAME.
2. Clique sur `Créer candidate naïve de démo`.
3. Clique sur `Comparer MAME vs candidate`.

La candidate naïve est volontairement fausse : elle sert juste à vérifier que les différences ressortent proprement.

Ensuite, la vraie étape consiste à brancher une candidate produite par le simulateur Godot réel.

## Étape 4 — Brancher la vraie logique Godot

Approche recommandée :

1. Copier ou lier dans ce projet les classes ennemies nécessaires du remake principal.
2. Écrire un adaptateur qui initialise l'état depuis `*_initial_snapshot.json`.
3. Avancer la simulation d'un tick fixe.
4. Produire un JSONL au même format que MAME.
5. Utiliser le comparateur intégré.

La séparation est importante : le labo n'a pas besoin du HUD, des collectibles, ni de toute la scène de jeu. Il doit seulement reproduire les états utiles au mouvement ennemi.

## Philosophie

Ce kit n'est pas un framework permanent de tests unitaires. C'est un microscope.

On commence avec une séquence simple : joueur immobile, premier ennemi sort de l'antre. Quand cette séquence matche, on ajoute :

- joueur qui bouge ;
- porte poussée ;
- fallback à une intersection ;
- rejet local de porte ;
- chase/BFS actif ;
- demi-tour forcé hors centre.
