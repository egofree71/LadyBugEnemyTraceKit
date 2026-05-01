# LadyBug Enemy Trace Kit - v14 overlay

Version 14 is a diagnostic step, not a new arcade AI candidate.

v13 proved that an enriched oracle key can replay enemy0 with zero movement differences on the current trace. v14 tries to move beyond the oracle by pointing the next MAME capture at the actual Z80 code path.

## Added buttons

- **Analyser plan Z80 v14**
  - Summarizes center decisions from the current frame_done trace.
  - Groups hot positions and PC pairs.
  - Lists the Z80 routines that should be traced next.

- **Écrire scripts MAME v14**
  - Writes these files under `res://data/mame/`:
    - `ladybug_enemy_v14_trace.lua`
    - `ladybug_enemy_v14_debugger_commands.txt`
    - `ladybug_enemy_v14_trace_points.md`

The debugger commands are a template. Depending on your MAME build/version, you may need to adjust breakpoint action syntax.

## Suggested flow

```text
Charger trace MAME
→ Analyser timing v13
→ Analyser plan Z80 v14
→ Écrire scripts MAME v14
```

Then use the generated files as a guide for a new MAME capture around:

```text
42BA Enemy_UpdateOne enter
42E6 TryPreferredDirection enter
430F preferred loaded into temp dir
4325 local door check
4334 fallback enter
4342 forced reversal test
4347 forced reversal hit
4224 move one pixel
43CE commit temp state
```

## Why v14 exists

The frame_done trace tells us what happened by the end of a frame, but not exactly which Z80 branch produced the decision. v14 prepares the next instrumentation pass so that future candidates can implement:

```text
preferred accepted
local door rejected
fallback used
forced reversal used
move one pixel
commit final state
```

instead of learning the final direction from the trace.
