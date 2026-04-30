# Trace schema

## Fichier initial

Nom recommandé :

```text
ladybug_enemy_trace_initial_snapshot.json
```

Schema :

```json
{
  "schema": "ladybug.enemyInitialSnapshot.v1",
  "mameVersion": "mame 0.xxx",
  "romName": "ladybug",
  "system": "Lady Bug",
  "saveStateRequested": "1",
  "ram6000_62AF": "...hex...",
  "logicalMaze6200_62AF": "...hex...",
  "vramD000_D3FF": "...hex...",
  "colorD400_D7FF": "...hex...",
  "doorTilesFromRomTable0D1D": [],
  "player": {},
  "enemies": [],
  "enemyWork": {},
  "timers": {},
  "ports": {}
}
```

## Trace JSONL

Nom recommandé :

```text
ladybug_enemy_trace_trace.jsonl
```

Une ligne = un échantillon :

```json
{
  "schema": "ladybug.enemyTrace.v1",
  "sample": 0,
  "mameFrame": 31,
  "pc": "07A9",
  "r": "12",
  "player": {
    "raw": "82",
    "x": "58",
    "y": "56",
    "turnTargetX": "58",
    "turnTargetY": "56",
    "currentDir": "08"
  },
  "enemies": [
    {
      "slot": 0,
      "addr": "602B",
      "raw": "82",
      "dir": "08",
      "collisionActive": true,
      "x": "58",
      "y": "57"
    }
  ],
  "enemyWork": {
    "tempDir": "08",
    "tempX": "58",
    "tempY": "57",
    "rejectedMask": "00",
    "fallbackMask": "00",
    "preferred": ["04", "02", "01", "08"],
    "chaseTimers": ["00", "00", "00", "00"],
    "chaseRoundRobin": "00"
  },
  "timers": {
    "61B4": "00",
    "61B5": "00",
    "61B6": "60",
    "61B7": "00",
    "61B8": "00",
    "61B9": "B4",
    "freeze61E1": "00"
  }
}
```

## Convention

Les bytes sont stockés en chaînes hexadécimales majuscules, sans `0x`, sur deux caractères.
Les adresses sont stockées sur quatre caractères.
