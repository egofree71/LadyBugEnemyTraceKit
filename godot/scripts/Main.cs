using System;
using System.Collections.Generic;
using Godot;
using LadyBugEnemyTraceLab.TraceLab;
using LadyBugEnemyTraceLab.TraceLab.Compare;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;
using LadyBugEnemyTraceLab.TraceLab.Sim;
using LadyBugEnemyTraceLab.TraceLab.Visual;

public partial class Main : Control
{
    private LineEdit _mameTracePath = null!;
    private LineEdit _candidateTracePath = null!;
    private LineEdit _z80EventsPath = null!;
    private TextEdit _log = null!;
    private EnemyPathView _pathView = null!;
    private VBoxContainer _legacyTools = null!;

    private List<ArcadeTraceFrame> _mameFrames = new();
    private List<ArcadeTraceFrame> _candidateFrames = new();
    private List<Z80EventFrame> _z80Events = new();

    public override void _Ready()
    {
        BuildUi();
        Log("LadyBug Enemy Trace Lab prêt.");
        Log("Étape actuelle: charger la trace MAME + events Z80, générer la candidate local-door/fallback v17, puis comparer enemy0.");
    }

    private void BuildUi()
    {
        var root = new VBoxContainer
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 12,
            OffsetTop = 12,
            OffsetRight = -12,
            OffsetBottom = -12
        };
        AddChild(root);

        var title = new Label
        {
            Text = "LadyBug Enemy Trace Lab",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(title);

        root.AddChild(new Label
        {
            Text = "Projet séparé pour analyser une trace MAME et comparer une trace candidate Godot. Tu peux lancer directement depuis l'éditeur Godot."
        });

        _mameTracePath = AddPathRow(root, "Trace MAME", "res://data/traces/ladybug_enemy_v15_trace_trace.jsonl", OnLoadMameTrace);
        _candidateTracePath = AddPathRow(root, "Trace candidate", "res://data/traces/godot_candidate_trace.jsonl", OnLoadCandidateTrace);
        _z80EventsPath = AddPathRow(root, "Trace Z80 events", "res://data/traces/ladybug_enemy_v15_trace_z80_events.jsonl", OnLoadZ80Events);

        var currentTitle = new Label
        {
            Text = "Flow principal v17"
        };
        currentTitle.AddThemeFontSizeOverride("font_size", 16);
        root.AddChild(currentTitle);

        var currentRow = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(currentRow);

        var analyzeV17Button = new Button { Text = "Analyser local-door/fallback v17" };
        analyzeV17Button.Pressed += OnAnalyzeZ80LocalDoorFallbackV17;
        currentRow.AddChild(analyzeV17Button);

        var localDoorFallbackV17Button = new Button { Text = "Générer candidate local-door/fallback v17" };
        localDoorFallbackV17Button.Pressed += OnCreateLocalDoorFallbackCandidateV17;
        currentRow.AddChild(localDoorFallbackV17Button);

        var z80GuidedMainButton = new Button { Text = "Générer candidate Z80-guided v16" };
        z80GuidedMainButton.Pressed += OnCreateZ80GuidedCandidateV16;
        currentRow.AddChild(z80GuidedMainButton);

        var compareMovementMainButton = new Button { Text = "Comparer mouvement enemy0" };
        compareMovementMainButton.Pressed += OnCompareEnemy0Movement;
        currentRow.AddChild(compareMovementMainButton);

        var compareFullMainButton = new Button { Text = "Comparer complet" };
        compareFullMainButton.Pressed += OnCompareFull;
        currentRow.AddChild(compareFullMainButton);

        var clearMainButton = new Button { Text = "Effacer log" };
        clearMainButton.Pressed += () => _log.Text = string.Empty;
        currentRow.AddChild(clearMainButton);

        var legacyToggle = new Button { Text = "Afficher / masquer anciens probes" };
        legacyToggle.Pressed += () => _legacyTools.Visible = !_legacyTools.Visible;
        root.AddChild(legacyToggle);

        _legacyTools = new VBoxContainer
        {
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(_legacyTools);

        var legacyTitle = new Label
        {
            Text = "Anciens probes / historique"
        };
        legacyTitle.AddThemeFontSizeOverride("font_size", 14);
        _legacyTools.AddChild(legacyTitle);

        var actionRow1 = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _legacyTools.AddChild(actionRow1);

        var makeCandidateButton = new Button { Text = "Créer candidate naïve de démo" };
        makeCandidateButton.Pressed += OnCreateNaiveCandidate;
        actionRow1.AddChild(makeCandidateButton);

        var pixelCenterButton = new Button { Text = "Générer candidate pixel+centres" };
        pixelCenterButton.Pressed += OnCreatePixelCenterCandidate;
        actionRow1.AddChild(pixelCenterButton);

        var staticMazeButton = new Button { Text = "Générer candidate static-maze" };
        staticMazeButton.Pressed += OnCreateStaticMazeCandidate;
        actionRow1.AddChild(staticMazeButton);

        var currentPrefButton = new Button { Text = "Générer candidate pref N+1" };
        currentPrefButton.Pressed += OnCreateStaticMazeCurrentPrefCandidate;
        actionRow1.AddChild(currentPrefButton);

        var corridorGatedButton = new Button { Text = "Générer candidate corridor-gated" };
        corridorGatedButton.Pressed += OnCreateCorridorGatedCandidate;
        actionRow1.AddChild(corridorGatedButton);

        var reversalProbeButton = new Button { Text = "Générer candidate reversal-probe v11" };
        reversalProbeButton.Pressed += OnCreateReversalProbeCandidate;
        actionRow1.AddChild(reversalProbeButton);

        var timingProbeButton = new Button { Text = "Générer candidate timing-probe v13" };
        timingProbeButton.Pressed += OnCreateTimingProbeCandidate;
        actionRow1.AddChild(timingProbeButton);

        var z80GuidedButton = new Button { Text = "Générer candidate Z80-guided v16" };
        z80GuidedButton.Pressed += OnCreateZ80GuidedCandidateV16;
        actionRow1.AddChild(z80GuidedButton);

        var centerOracleButton = new Button { Text = "Générer candidate oracle-centres" };
        centerOracleButton.Pressed += OnCreateCenterOracleCandidate;
        actionRow1.AddChild(centerOracleButton);

        var learnedButton = new Button { Text = "Générer candidate replay guidé" };
        learnedButton.Pressed += OnCreateLearnedDecisionCandidate;
        actionRow1.AddChild(learnedButton);

        var actionRow2 = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _legacyTools.AddChild(actionRow2);

        var analyzeButton = new Button { Text = "Analyser décisions MAME" };
        analyzeButton.Pressed += OnAnalyzeMameDecisions;
        actionRow2.AddChild(analyzeButton);

        var conflictButton = new Button { Text = "Analyser conflits décisionnels" };
        conflictButton.Pressed += OnAnalyzeDecisionConflicts;
        actionRow2.AddChild(conflictButton);

        var timingButton = new Button { Text = "Analyser timing préférences" };
        timingButton.Pressed += OnAnalyzePreferenceTiming;
        actionRow2.AddChild(timingButton);

        var timingProbeAnalyzeButton = new Button { Text = "Analyser timing v13" };
        timingProbeAnalyzeButton.Pressed += OnAnalyzePreferenceTimingProbe;
        actionRow2.AddChild(timingProbeAnalyzeButton);

        var z80LocalFallbackAnalyzeButton = new Button { Text = "Analyser local-door/fallback v16" };
        z80LocalFallbackAnalyzeButton.Pressed += OnAnalyzeZ80LocalDoorFallbackV16;
        actionRow2.AddChild(z80LocalFallbackAnalyzeButton);

        var reversalAnalyzeButton = new Button { Text = "Analyser demi-tours MAME" };
        reversalAnalyzeButton.Pressed += OnAnalyzeForcedReversals;
        actionRow2.AddChild(reversalAnalyzeButton);

        var compareMovementButton = new Button { Text = "Comparer mouvement enemy0" };
        compareMovementButton.Pressed += OnCompareEnemy0Movement;
        actionRow2.AddChild(compareMovementButton);

        var compareButton = new Button { Text = "Comparer complet" };
        compareButton.Pressed += OnCompareFull;
        actionRow2.AddChild(compareButton);

        var clearButton = new Button { Text = "Effacer log" };
        clearButton.Pressed += () => _log.Text = string.Empty;
        actionRow2.AddChild(clearButton);

        var split = new HSplitContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(split);

        _pathView = new EnemyPathView
        {
            CustomMinimumSize = new Vector2(500, 500),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        split.AddChild(_pathView);

        _log = new TextEdit
        {
            Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        split.AddChild(_log);
    }

    private LineEdit AddPathRow(VBoxContainer root, string labelText, string defaultPath, Action onButtonPressed)
    {
        var row = new HBoxContainer();
        root.AddChild(row);

        row.AddChild(new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(120, 0)
        });

        var lineEdit = new LineEdit
        {
            Text = defaultPath,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddChild(lineEdit);

        var button = new Button { Text = "Charger" };
        button.Pressed += onButtonPressed;
        row.AddChild(button);

        return lineEdit;
    }

    private void OnLoadMameTrace()
    {
        try
        {
            TraceLoadResult result = TraceJsonlReader.Load(_mameTracePath.Text);
            _mameFrames = result.Frames;
            _pathView.SetTrace(_mameFrames, 0);
            Log($"Trace MAME chargée: {result.Frames.Count} samples depuis {result.Path}");
            foreach (string warning in result.Warnings)
                Log("  warning: " + warning);
            LogFirstFrameSummary(_mameFrames, "MAME");
        }
        catch (Exception ex)
        {
            Log("ERREUR chargement MAME: " + ex.Message);
        }
    }

    private void OnLoadCandidateTrace()
    {
        try
        {
            TraceLoadResult result = TraceJsonlReader.Load(_candidateTracePath.Text);
            _candidateFrames = result.Frames;
            _pathView.SetTrace(_candidateFrames, 0);
            Log($"Trace candidate chargée: {result.Frames.Count} samples depuis {result.Path}");
            foreach (string warning in result.Warnings)
                Log("  warning: " + warning);
            LogFirstFrameSummary(_candidateFrames, "Candidate");
        }
        catch (Exception ex)
        {
            Log("ERREUR chargement candidate: " + ex.Message);
        }
    }


    private void OnLoadZ80Events()
    {
        try
        {
            Z80EventLoadResult result = Z80EventJsonlReader.Load(_z80EventsPath.Text);
            _z80Events = result.Events;
            Log($"Trace Z80 events chargée: {result.Events.Count} events depuis {result.Path}");
            foreach (string warning in result.Warnings)
                Log("  warning: " + warning);

            if (_z80Events.Count > 0)
            {
                Z80EventFrame first = _z80Events[0];
                Z80EventFrame last = _z80Events[^1];
                Log($"Z80 first: sample={first.FrameSample}, frame={first.MameFrame}, tag={first.Tag}, pc={first.Pc}");
                Log($"Z80 last : sample={last.FrameSample}, frame={last.MameFrame}, tag={last.Tag}, pc={last.Pc}");
            }
        }
        catch (Exception ex)
        {
            Log("ERREUR chargement Z80 events: " + ex.Message);
        }
    }

    private bool EnsureMameTraceLoaded()
    {
        if (_mameFrames.Count == 0)
            OnLoadMameTrace();

        if (_mameFrames.Count > 0)
            return true;

        Log("Action impossible: aucune trace MAME chargée.");
        return false;
    }


    private bool EnsureZ80EventsLoaded()
    {
        if (_z80Events.Count == 0)
            OnLoadZ80Events();

        if (_z80Events.Count > 0)
            return true;

        Log("Action impossible: aucune trace Z80 events chargée.");
        return false;
    }

    private void OnCreateNaiveCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        _candidateFrames = NaiveCandidateTraceFactory.CreateFromMameShape(_mameFrames);
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);
        Log($"Candidate naïve écrite: {_candidateTracePath.Text}");
        Log("Cette candidate sert seulement à vérifier le pipeline de diff; elle n'est PAS l'IA arcade.");
    }

    private void OnCreatePixelCenterCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = CandidateEnemySimulator.CreatePixelCenterCandidate(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate pixel+centres écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateStaticMazeCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = CandidateEnemySimulator.CreateStaticMazeCandidate(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate static-maze écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateStaticMazeCurrentPrefCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = CandidateEnemySimulator.CreateStaticMazeCurrentPrefCandidate(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate pref N+1 écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateCorridorGatedCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = CorridorGatedCandidateTraceFactory.Create(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate corridor-gated écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateReversalProbeCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = LearnedReversalProbeCandidateTraceFactory.Create(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate reversal-probe v11 écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateTimingProbeCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = TimingProbeCandidateTraceFactory.Create(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate timing-probe v13 écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }


    private void OnCreateZ80GuidedCandidateV16()
    {
        if (!EnsureMameTraceLoaded() || !EnsureZ80EventsLoaded())
            return;

        CandidateGenerationResult result = Z80EventGuidedCandidateTraceFactory.Create(_mameFrames, _z80Events);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate Z80-guided v16 écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateLocalDoorFallbackCandidateV17()
    {
        if (!EnsureMameTraceLoaded() || !EnsureZ80EventsLoaded())
            return;

        CandidateGenerationResult result = LocalDoorFallbackCandidateTraceFactoryV17.Create(_mameFrames, _z80Events);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate local-door/fallback v17 écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateCenterOracleCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = CandidateEnemySimulator.CreateCenterOracleCandidate(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate oracle-centres écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }

    private void OnCreateLearnedDecisionCandidate()
    {
        if (!EnsureMameTraceLoaded())
            return;

        CandidateGenerationResult result = CandidateEnemySimulator.CreateLearnedDecisionCandidate(_mameFrames);
        _candidateFrames = result.Frames;
        TraceJsonlWriter.Write(_candidateTracePath.Text, _candidateFrames);
        _pathView.SetTrace(_candidateFrames, 0);

        Log($"Candidate replay guidé écrite: {_candidateTracePath.Text}");
        foreach (string message in result.Messages)
            Log("  " + message);
    }


    private void OnAnalyzeZ80LocalDoorFallbackV17()
    {
        if (!EnsureMameTraceLoaded() || !EnsureZ80EventsLoaded())
            return;

        foreach (string line in Z80LocalDoorFallbackAnalyzerV17.BuildReport(_mameFrames, _z80Events))
            Log(line);
    }

    private void OnAnalyzeZ80LocalDoorFallbackV16()
    {
        if (!EnsureMameTraceLoaded() || !EnsureZ80EventsLoaded())
            return;

        foreach (string line in Z80LocalDoorFallbackAnalyzer.BuildReport(_mameFrames, _z80Events))
            Log(line);
    }

    private void OnAnalyzeMameDecisions()
    {
        if (!EnsureMameTraceLoaded())
            return;

        foreach (string line in DecisionTraceAnalyzer.BuildReport(_mameFrames))
            Log(line);
    }

    private void OnAnalyzeDecisionConflicts()
    {
        if (!EnsureMameTraceLoaded())
            return;

        foreach (string line in DecisionConflictAnalyzer.BuildVisibleKeyConflictReport(_mameFrames))
            Log(line);
    }

    private void OnAnalyzePreferenceTiming()
    {
        if (!EnsureMameTraceLoaded())
            return;

        foreach (string line in DecisionTimingAnalyzer.BuildReport(_mameFrames))
            Log(line);
    }

    private void OnAnalyzePreferenceTimingProbe()
    {
        if (!EnsureMameTraceLoaded())
            return;

        foreach (string line in PreferenceTimingProbeAnalyzer.BuildReport(_mameFrames))
            Log(line);
    }

    private void OnAnalyzeForcedReversals()
    {
        if (!EnsureMameTraceLoaded())
            return;

        foreach (string line in ForcedReversalAnalyzer.BuildReport(_mameFrames))
            Log(line);
    }

    private void OnCompareEnemy0Movement()
    {
        if (!EnsureBothTracesLoaded())
            return;

        TraceComparisonSummary summary = TraceComparer.CompareEnemy0MovementOnly(_mameFrames, _candidateFrames, 300);
        LogComparisonSummary("--- COMPARAISON MOUVEMENT ENEMY0 ---", summary);
    }

    private void OnCompareFull()
    {
        if (!EnsureBothTracesLoaded())
            return;

        TraceComparisonSummary summary = TraceComparer.Compare(_mameFrames, _candidateFrames, 300);
        LogComparisonSummary("--- COMPARAISON COMPLÈTE ---", summary);
    }

    private bool EnsureBothTracesLoaded()
    {
        if (_mameFrames.Count == 0)
            OnLoadMameTrace();

        if (_candidateFrames.Count == 0)
            OnLoadCandidateTrace();

        if (_mameFrames.Count > 0 && _candidateFrames.Count > 0)
            return true;

        Log("Comparaison impossible: il faut une trace MAME et une trace candidate.");
        return false;
    }

    private void LogComparisonSummary(string title, TraceComparisonSummary summary)
    {
        Log(title);
        Log($"Samples comparés: {summary.ComparedSamples}");
        Log($"Différences totales: {summary.DifferenceCount}");
        if (summary.HasLengthMismatch)
            Log($"Longueurs différentes: MAME={summary.MameLength}, candidate={summary.CandidateLength}");

        if (summary.FirstDifferences.Count == 0)
        {
            Log("Aucune différence sur les champs comparés. Champagne prudent, mais champagne quand même.");
            return;
        }

        Log("Premières différences:");
        foreach (TraceDifference diff in summary.FirstDifferences)
            Log("  " + diff);
    }

    private void LogFirstFrameSummary(IReadOnlyList<ArcadeTraceFrame> frames, string label)
    {
        if (frames.Count == 0)
            return;

        ArcadeTraceFrame first = frames[0];
        Log($"{label} first sample={first.Sample}, pc={first.Pc}, R={first.R}, player=({first.Player.X},{first.Player.Y}) dir={first.Player.CurrentDir}");
        if (first.Enemies.Count > 0)
        {
            EnemySlotFrame e0 = first.Enemies[0];
            Log($"{label} enemy0 raw={e0.Raw}, pos=({e0.X},{e0.Y}), dir={e0.Dir}, active={e0.CollisionActive}");
        }
    }

    private void Log(string message)
    {
        GD.Print(message);
        if (_log is not null)
            _log.Text += message + System.Environment.NewLine;
    }
}
