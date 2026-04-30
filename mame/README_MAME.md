# MAME capture

## 1. Préparer le save state

Dans MAME, démarre `ladybug`, arrive au point qui t'intéresse, puis sauvegarde un état.
Pour le premier test, l'idéal est un état juste avant la première sortie de l'antre ou juste après l'activation du premier ennemi.

Si l'état est sauvegardé comme :

```text
sta/ladybug/test1.sta
```

alors garde simplement ce nom dans le script :

```lua
save_state = "test1"
```

## 2. Configurer le script

Ouvre `ladybug_enemy_trace.lua` et modifie en haut :

```lua
save_state = "test1"
frames_to_capture = 900
output_prefix = "ladybug_enemy_trace"
output_dir = "." -- ou par exemple "C:/temp/ladybug-traces"
```

Si `save_state` reste vide, le script capture l'état courant après un petit délai.

## 3. Lancer MAME

Exemple Windows, depuis le dossier de MAME :

```bat
mame ladybug -window -console -autoboot_script C:\Users\Philippe-utilisateur\Documents\Godot\LadyBugEnemyTraceKit\mame\ladybug_enemy_trace.lua -autoboot_delay 1
```

Important : `-autoboot_delay` attend un nombre de secondes. Il ne faut pas y mettre le nom du save state.
Le save state est chargé par la variable `save_state` dans le script Lua.

Le script produit :

```text
ladybug_enemy_trace_initial_snapshot.json
ladybug_enemy_trace_trace.jsonl
ladybug_enemy_trace_summary.txt
```

Copie ensuite les deux fichiers JSON/JSONL dans :

```text
godot/data/traces/
```

Puis ouvre le projet Godot du dossier `godot/`.
