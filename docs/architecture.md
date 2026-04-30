# Architecture proposée

## Objectif

Créer un labo séparé du remake principal pour comparer :

```text
MAME / arcade original = oracle
Godot / simulation candidate = hypothèse
Diff automatique = diagnostic
```

Le premier scénario visé est volontairement minimal : joueur immobile, premier ennemi qui sort de l'antre.

## Pourquoi un labo séparé ?

Le but n'est pas d'ajouter une suite de tests permanente au jeu principal. Le but est d'obtenir une preuve ponctuelle et précise sur le mouvement ennemi.

Le labo doit donc être léger :

- pas de HUD complet ;
- pas de score ;
- pas de sons ;
- pas de collectibles au départ ;
- uniquement les données utiles au mouvement ennemi.

## Pipeline

```text
1. MAME Lua
   charge éventuellement un save state
   capture un snapshot initial
   capture une trace JSONL frame/tick par frame/tick

2. Godot Trace Lab
   lit la trace MAME
   visualise enemy0
   lit ou produit une trace candidate
   compare les champs importants

3. Analyse
   première divergence = endroit à inspecter
```

## Données capturées côté MAME

### Snapshot initial

Le snapshot initial contient :

- RAM 6000..62AF ;
- logical maze 6200..62AF ;
- VRAM D000..D3FF ;
- color RAM D400..D7FF ;
- positions joueur et ennemis ;
- timers ennemis ;
- préférences ennemies ;
- état temporaire de mouvement ;
- tuiles de portes extraites depuis la table ROM 0x0D1D.

### Trace JSONL

Chaque ligne représente un échantillon :

- PC et registre R si disponibles ;
- joueur : raw, x, y, currentDir, turn target ;
- ennemis : raw, dir, x, y, active ;
- work RAM ennemi : 61BD..61C2 ;
- preferred dirs : 61C4..61C7 ;
- chase timers : 61CE..61D1 ;
- timers : 61B4..61B9, 61E1.

## Architecture Godot du labo

```text
Main.cs
  construit l'interface
  charge les traces
  lance la comparaison

TraceLab/Model
  classes C# correspondant au JSONL

TraceLab/Io
  lecteur JSONL
  writer JSONL
  helpers hexadécimaux

TraceLab/Compare
  comparaison champ par champ

TraceLab/Visual
  visualiseur de chemin enemy0
```

## Étape suivante pour brancher la vraie IA

Créer un adaptateur :

```text
ArcadeInitialSnapshot -> état interne Godot
AdvanceOneArcadeTick()
état interne Godot -> ArcadeTraceFrame
```

Cet adaptateur doit être la seule couche qui dépend de la vraie implémentation ennemie.

## Ordre de validation recommandé

1. Position initiale du joueur.
2. Position initiale de l'ennemi visible dans l'antre.
3. Cadence de release.
4. Premier mouvement pixel par pixel.
5. Premier centre de décision.
6. Direction préférée lue par l'ennemi.
7. Validation logique de la cellule.
8. Validation locale de porte.
9. Fallback.
10. Chase/BFS.


## Version v4 - candidate simulation

Cette version ajoute deux générateurs de trace candidate côté Godot :

- **pixel+centres** : simulation minimale. L'ennemi avance d'un pixel et prend `preferred[0]` aux centres de décision. Cette version doit diverger tôt, car elle ne valide pas encore les portes, le labyrinthe ni les fallbacks.
- **replay guidé** : simulation qui apprend les décisions observées dans la trace MAME complète. Elle sert de contrôle du pipeline et d'outil d'analyse, pas d'IA finale.

Le bouton **Analyser décisions MAME** imprime les centres de décision et les changements de direction observés.

## v6 note: static-maze candidate

The v6 Godot lab adds `Générer candidate static-maze`.

This candidate is still not a full arcade enemy AI. It ports the static direction-mask validation from the Z80 routine at `0x3911`, using the compact ROM table at `0x0DA2..0x0DE3`.

At a monster decision center it now does:

1. Try `preferred[0]`.
2. Accept it only if the static mask allows the direction.
3. Otherwise keep the current direction if still allowed.
4. Otherwise choose the first allowed fallback in arcade order `01, 02, 04, 08`.

It intentionally does not yet emulate the local door/tile validation at `0x4130` or the forced door reversal at `0x4189/0x4347`.

## v7 note: decision conflict diagnostics

The v7 lab adds two diagnostic tools:

- `Analyser conflits décisionnels`: groups MAME decision centers by the visible key `(x,y,currentDir,preferred0)` and reports cases where the same visible key leads to different next directions. This exposes hidden arcade state dependencies.
- `Générer candidate oracle-centres`: uses a richer learned key `(x,y,currentDir,preferred[0..3],chaseTimers,61B6..61B9)` only at decision centers, then performs normal one-pixel movement. This is not an AI implementation; it is a control candidate to confirm that remaining divergence is decision logic, not pixel movement or trace timing.

The first important trace conflict observed is around `(58,96), dir=08, pref0=01`: once the arcade chooses left, another time it chooses up. Therefore a simple static-maze rule cannot be the full model.


## v8 - diagnostic timing des préférences

La v8 ajoute deux outils:

- `Analyser timing préférences`: compare la direction réellement prise par MAME avec `preferred[0]` du sample courant et du sample suivant.
- `Générer candidate pref N+1`: teste l'hypothèse selon laquelle une transition `sample N -> N+1` utilise la préférence visible dans le sample `N+1`, parce que la génération de préférences arrive avant `Enemy_UpdateAll`, alors que le logger frame_done voit l'état après coup.

Résultat attendu sur la trace fournie: cette candidate doit dépasser la divergence sample 97 de `static-maze`, mais peut encore diverger plus tard. Une divergence résiduelle indique que la trace frame_done ne suffit pas à connaître avec certitude la préférence utilisée au moment exact de chaque centre, ou qu'il manque encore la validation locale `0x4130`.
