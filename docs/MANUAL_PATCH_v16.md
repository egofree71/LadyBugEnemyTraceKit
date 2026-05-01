# Patch manuel v16

Copie ces fichiers dans le dépôt :

```text
godot/scripts/Main.cs
godot/scripts/TraceLab/Model/Z80EventModels.cs
godot/scripts/TraceLab/Io/Z80EventJsonlReader.cs
godot/scripts/TraceLab/Compare/Z80EventCycle.cs
godot/scripts/TraceLab/Compare/Z80LocalDoorFallbackAnalyzer.cs
godot/scripts/TraceLab/Sim/Z80EventGuidedCandidateTraceFactory.cs
README_v16.md
```

Puis reconstruis le projet Godot/.NET.

La v16 ne remplace pas encore l'IA ennemie finale : elle transforme la trace Z80 en diagnostic lisible et en candidate guidée par `MoveOnePixel_4224`.
