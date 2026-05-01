# Manual patch v12

In `godot/scripts/Main.cs`:

1. Change the default MAME trace path to:

```csharp
"res://data/traces/ladybug_enemy_trace_trace.jsonl"
```

2. Replace both action rows from `HBoxContainer` to `HFlowContainer`:

```csharp
var actionRow1 = new HFlowContainer
{
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
root.AddChild(actionRow1);
```

and:

```csharp
var actionRow2 = new HFlowContainer
{
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
root.AddChild(actionRow2);
```

This makes long button rows wrap instead of being clipped horizontally.
