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
    private TextEdit _log = null!;
    private EnemyPathView _pathView = null!;

    private List<ArcadeTraceFrame> _mameFrames = new();
    private List<ArcadeTraceFrame> _candidateFrames = new();

    public override void _Ready()
    {
        BuildUi();
        Log("LadyBug Enemy Trace Lab prêt.");
        Log("Étape actuelle: charger la trace MAME, générer une candidate expérimentale, puis comparer le mouvement enemy0.");
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

        _mameTracePath = AddPathRow(root, "Trace MAME", "res://data/traces/ladybug_enemy_trace_trace.jsonl", OnLoadMameTrace);
        _candidateTracePath = AddPathRow(root, "Trace candidate", "res://data/traces/godot_candidate_trace.jsonl", OnLoadCandidateTrace);

        var actionRow1 = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(actionRow1);

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
        root.AddChild(actionRow2);

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

    private bool EnsureMameTraceLoaded()
    {
        if (_mameFrames.Count == 0)
            OnLoadMameTrace();

        if (_mameFrames.Count > 0)
            return true;

        Log("Action impossible: aucune trace MAME chargée.");
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
