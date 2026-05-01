# Manual patch v14

Copy the overlay files into the repository root.

Files added:

```text
godot/scripts/TraceLab/Compare/CodePathTracePlanAnalyzer.cs
godot/scripts/TraceLab/Io/MameLuaTraceScriptWriter.cs
README_v14.md
docs/MANUAL_PATCH_v14.md
```

File replaced:

```text
godot/scripts/Main.cs
```

The Main.cs replacement only adds two buttons and two handlers on top of the committed v13 UI:

```text
Analyser plan Z80 v14
Écrire scripts MAME v14
```

If you prefer a manual edit instead of replacing Main.cs, add these calls in the second HFlowContainer:

```csharp
var z80PlanButton = new Button { Text = "Analyser plan Z80 v14" };
z80PlanButton.Pressed += OnAnalyzeCodePathTracePlan;
actionRow2.AddChild(z80PlanButton);

var writeMameScriptButton = new Button { Text = "Écrire scripts MAME v14" };
writeMameScriptButton.Pressed += OnWriteMameV14TraceScripts;
actionRow2.AddChild(writeMameScriptButton);
```

And add these methods:

```csharp
private void OnAnalyzeCodePathTracePlan()
{
    if (!EnsureMameTraceLoaded())
        return;

    foreach (string line in CodePathTracePlanAnalyzer.BuildReport(_mameFrames))
        Log(line);
}

private void OnWriteMameV14TraceScripts()
{
    if (!EnsureMameTraceLoaded())
        return;

    try
    {
        foreach (string line in MameLuaTraceScriptWriter.WriteDefaultFiles(_mameFrames))
            Log(line);
    }
    catch (Exception ex)
    {
        Log("ERREUR écriture scripts MAME v14: " + ex.Message);
    }
}
```
