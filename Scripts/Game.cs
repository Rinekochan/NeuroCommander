using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using NeuroWarCommander.Scripts.Team;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Team.Blackboard;
using FileAccess = Godot.FileAccess;

public partial class Game : Node2D
{
    public enum GameMap
    {
        Map1,
        Map2
    }
    [Export] public float MatchTimeLimit { get; set; } = 300.0f; // 5 minutes max per match
    [Export] public int NumMatchesToRun { get; set; } = 20; // Number of matches to run automatically
    [Export] public GameMap Map { get; set; } = GameMap.Map1;
    [Export] public float DataCollectionInterval { get; set; } = 10.0f; // Collect data every 10 seconds

    private Node2D _world;
    private Node _ui;
    private Team _teamA;
    private Team _teamB;
    private float _elapsedTime;
    private int _matchCount;
    private string _currentMap;
    private float _timeSinceLastDataCollection;
    private string _logFilePath;
    private StringBuilder _matchLog;
    private List<Dictionary<string, object>> _timeSeriesData;
    private Random _random = new();
    private string _winReason;

    // For analysis
    private Dictionary<string, int> _combatEncounters = new();
    private Dictionary<string, float> _decisionTimes = new();
    private Dictionary<string, float> _reactionTimes = new();
    private Dictionary<ulong, string> _currentTeamNames = new();

    // Timestamps for reaction time measurement
    private Dictionary<string, float> _lastEnemySpottedTime = new();
    private Dictionary<string, float> _lastActionTime = new();

    private bool _isPaused = false;
    private CanvasLayer _pauseOverlay;

    public override void _Ready()
    {
        GD.Print("Game initializing...");

        _world = GetNode<Node2D>("World");
        _ui = GetNode("UI");
        ProcessMode = ProcessModeEnum.Always;

        // Ensure logs directory exists
        var dir = DirAccess.Open("res://");
        if (!dir.DirExists("MatchLogs"))
        {
            dir.MakeDir("MatchLogs");

            using var gdignore = FileAccess.Open("res://MatchLogs/.gdignore", FileAccess.ModeFlags.Write);
            gdignore.StoreString("");
        }

        SetupPauseOverlay();

        // Initialize the match
        StartNewMatch(resetWorld: false);
    }

    public override void _Process(double delta)
    {
        if (_isPaused) return;

        _elapsedTime += (float)delta;
        _timeSinceLastDataCollection += (float)delta;

        // Collect data at regular intervals
        if (_timeSinceLastDataCollection >= DataCollectionInterval)
        {
            CollectTimeSeriesData();
            _timeSinceLastDataCollection = 0;
        }

        // Check for match end conditions
        if (CheckMatchEndConditions())
        {
            EndCurrentMatch();

            GetTree().Quit(); // Exit after running all matches
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed && !eventKey.IsEcho())
        {
            if (eventKey.Keycode == Key.Space)
            {
                TogglePause();
            }
        }
    }

    private void SetupPauseOverlay()
    {
        // Create pause overlay
        _pauseOverlay = new CanvasLayer();
        _pauseOverlay.Name = "PauseOverlay";
        _pauseOverlay.Layer = 10; // Set high layer to be on top of other UI
        _pauseOverlay.ProcessMode = Node.ProcessModeEnum.Always;
        AddChild(_pauseOverlay);

        // Create panel for pause text
        var pausePanel = new Panel();
        pausePanel.AnchorRight = 1;
        pausePanel.AnchorBottom = 1;
        pausePanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pausePanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _pauseOverlay.AddChild(pausePanel);

        // Add pause text label
        var pauseLabel = new Label();
        pauseLabel.Text = "PAUSED\nPress SPACEBAR to continue";
        pauseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        pauseLabel.VerticalAlignment = VerticalAlignment.Center;
        pauseLabel.AnchorRight = 1;
        pauseLabel.AnchorBottom = 1;
        pauseLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pauseLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        pausePanel.AddChild(pauseLabel);

        // Hide overlay by default
        _pauseOverlay.Visible = false;
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            // Store current process mode and set to disabled
            if (_world != null)
                _world.ProcessMode = ProcessModeEnum.Disabled;

            GD.Print("Game paused");
        }
        else
        {
            // Restore process mode to inherit
            if (_world != null)
                _world.ProcessMode = ProcessModeEnum.Inherit;

            GD.Print("Game resumed");
        }

        _pauseOverlay.Visible = _isPaused;

        if (_isPaused)
            GD.Print("Game paused");
        else
            GD.Print("Game resumed");
    }

    private void StartNewMatch(bool resetWorld = true)
    {
        GD.Print($"Starting match {_matchCount + 1} of {NumMatchesToRun}");

        // Select map based on enum
        _currentMap = Map switch
        {
            GameMap.Map1 => "Map1",
            GameMap.Map2 => "Map2",
            _ => "Map1"
        };

        // Reset match state
        _elapsedTime = 0;
        _timeSinceLastDataCollection = 0;
        _timeSeriesData = new List<Dictionary<string, object>>();
        _matchLog = new StringBuilder();

        // Create unique log file name with timestamp
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = $"res://MatchLogs/match_{_currentMap}_{timestamp}_#{_matchCount + 1}.csv";

        // Write header to log
        _matchLog.AppendLine($"Match #{_matchCount + 1}");
        _matchLog.AppendLine($"Map: {_currentMap}");
        _matchLog.AppendLine($"Start Time: {DateTime.Now}");
        _matchLog.AppendLine("---");

        if (resetWorld)
        {
            // Remove existing world and UI
            if (_world != null)
                _world.QueueFree();
            if (_ui != null)
                _ui.QueueFree();

            // Create new world
            _world = GD.Load<Node2D>($"res://Scenes/World/world.tscn");
            AddChild(_world);

            // Create new UI
            _ui = GD.Load<Node2D>($"res://Scenes/UI/UI.tscn");
            AddChild(_ui);

            // Reset team references
        }
        _teamA = _world.GetNode<Team>("TeamA");
        _teamB = _world.GetNode<Team>("TeamB");

        QueueRedraw();

        // Reset analysis data
        _combatEncounters.Clear();
        _combatEncounters["TeamA"] = 0;
        _combatEncounters["TeamB"] = 0;

        _decisionTimes.Clear();
        _decisionTimes["TeamA"] = 0;
        _decisionTimes["TeamB"] = 0;

        _reactionTimes.Clear();
        _reactionTimes["TeamA"] = 0;
        _reactionTimes["TeamB"] = 0;

        _lastEnemySpottedTime.Clear();
        _lastActionTime.Clear();

        // Connect signals
        ConnectSignals();

        // Log AI types being used
        string teamAType = GetTeamAIType(_teamA);
        string teamBType = GetTeamAIType(_teamB);
        _matchLog.AppendLine($"Team A using {teamAType}");
        _matchLog.AppendLine($"Team B using {teamBType}");
        _matchLog.AppendLine("---");

        _matchCount++;
    }

    private void EndCurrentMatch()
    {
        string winner = DetermineWinner();
        float matchDuration = _elapsedTime;

        // Log match results
        GD.Print($"Match {_matchCount} ended. Winner: {winner}. Duration: {matchDuration}s");

        _matchLog.AppendLine($"Match ended after {matchDuration:F1} seconds");
        _matchLog.AppendLine($"Winner: {winner} - {_winReason}");

        // Calculate additional metrics
        CalculateFinalMetrics(winner);

        // Write time series data
        _matchLog.AppendLine("\n--- Time Series Data ---");
        _matchLog.AppendLine("Time,TeamA_Units,TeamB_Units,TeamA_Health%,TeamB_Health%,TeamA_CombatEncounters,TeamB_CombatEncounters,TeamA_AvgReactionTime,TeamB_AvgReactionTime");

        foreach (var dataPoint in _timeSeriesData)
        {
            _matchLog.AppendLine(string.Join(",", dataPoint.Select(kvp => kvp.Value)));
        }

        // Save log file
        SaveLogFile();

        // Disconnect signals
        DisconnectSignals();
    }

    private bool CheckMatchEndConditions()
    {
        // Check if time limit exceeded
        if (_elapsedTime >= MatchTimeLimit)
        {
            _winReason = "Time limit reached. Better resources.";
            return true;
        }

        // Check if either team is eliminated
        bool teamAEliminated = IsTeamEliminated(_teamA);
        bool teamBEliminated = IsTeamEliminated(_teamB);

        if (teamAEliminated || teamBEliminated)
        {
            return true;
        }

        return false;
    }

    private bool IsTeamEliminated(Team team)
    {
        // A team is eliminated if it has no units or their commander is dead
        int unitCount = team.GetNode("Units").GetChildCount();

        int commanderCount = team.GetNode("Units").GetChildren()
            .OfType<UnitBase>()
            .Count(u => u.IsInGroup("commanders") && u.CurrentHealth > 0);

        if (unitCount == 0)
            _winReason = $"{team.Name} has no units left.";
        else if (commanderCount == 0)
            _winReason = $"{team.Name} has no commanders left.";

        return unitCount == 0 || commanderCount == 0;
    }

    private string DetermineWinner()
    {
        bool teamAEliminated = IsTeamEliminated(_teamA);
        bool teamBEliminated = IsTeamEliminated(_teamB);

        if (teamBEliminated && !teamAEliminated)
        {
            return "TeamA";
        }
        if (teamAEliminated && !teamBEliminated)
        {
            return "TeamB";
        }
        else
        {
            // Time limit reached or both teams eliminated, determine winner by remaining resources
            int teamAStrength = CalculateTeamStrength(_teamA);
            int teamBStrength = CalculateTeamStrength(_teamB);

            if (teamAStrength > teamBStrength)
            {
                return "TeamA";
            }
            else if (teamBStrength > teamAStrength)
            {
                return "TeamB";
            }
            else
            {
                return "Draw";
            }
        }
    }

    private int CalculateTeamStrength(Team team)
    {
        int strength = 0;

        // Count units and their health
        var units = team.GetNode("Units").GetChildren().OfType<UnitBase>().ToList();
        foreach (var unit in units)
        {
            strength += (int)(unit.CurrentHealth / unit.MaxHealth * 100);
        }

        // Count camps
        var camps = GetAllCamps().Where(c => (int)c.TeamId == team.TeamId).ToList();
        foreach (var camp in camps)
        {
            strength += (int)(camp.CurrentHealth / camp.MaxHealth * 300); // Camps are weighted more
        }

        return strength;
    }

    private void CollectTimeSeriesData()
    {
        var dataPoint = new Dictionary<string, object>
        {
            { "Time", _elapsedTime.ToString("F1") },
            { "TeamA_Units", GetTeamUnitCount(_teamA) },
            { "TeamB_Units", GetTeamUnitCount(_teamB) },
            { "TeamA_Health%", GetTeamHealthPercentage(_teamA).ToString("F1") },
            { "TeamB_Health%", GetTeamHealthPercentage(_teamB).ToString("F1") },
            { "TeamA_CombatEncounters", _combatEncounters["TeamA"] },
            { "TeamB_CombatEncounters", _combatEncounters["TeamB"] },
            { "TeamA_AvgReactionTime", _reactionTimes["TeamA"] > 0 ? _reactionTimes["TeamA"].ToString("F2") : "N/A" },
            { "TeamB_AvgReactionTime", _reactionTimes["TeamB"] > 0 ? _reactionTimes["TeamB"].ToString("F2") : "N/A" }
        };

        _timeSeriesData.Add(dataPoint);
    }

    private int GetTeamUnitCount(Team team)
    {
        return team.GetNode("Units").GetChildCount();
    }

    private float GetTeamHealthPercentage(Team team)
    {
        var units = team.GetNode("Units").GetChildren().OfType<UnitBase>().ToList();

        if (units.Count == 0) return 0;

        float totalCurrentHealth = units.Sum(u => u.CurrentHealth);
        float totalMaxHealth = units.Sum(u => u.MaxHealth);

        return totalCurrentHealth / totalMaxHealth * 100;
    }

    private List<CampBase> GetAllCamps()
    {
        var camps = new List<CampBase>();

        // Find all camps in the map
        var map = _world.GetNode("Map/Camps");
        if (map != null)
        {
            foreach (var child in map.GetChildren())
            {
                if (child is CampBase camp)
                {
                    camps.Add(camp);
                }
            }
        }

        return camps;
    }

    private int GetScoutedCellsTeamA()
    {
        // Get the VisionMap for Team A
        var visionMap = _teamA.GetNodeOrNull<VisionMap>("Blackboard/VisionMap");
        if (visionMap == null) return 0;

        // Count cells that have been scouted by Team A
        return visionMap.VisionCells.Count(c => c.Value < float.MaxValue);
    }

    private int GetScoutedCellsTeamB()
    {
        // Get the VisionMap for Team B
        var visionMap = _teamB.GetNodeOrNull<VisionMap>("Blackboard/VisionMap");
        if (visionMap == null) return 0;

        // Count cells that have been scouted by Team A
        return visionMap.VisionCells.Count(c => c.Value < float.MaxValue);
    }

    private void SaveLogFile()
    {
        using (var file = FileAccess.Open(_logFilePath, FileAccess.ModeFlags.Write))
        {
            if (file != null)
            {
                file.StoreString(_matchLog.ToString());
                GD.Print($"Match log saved to {_logFilePath}");
            }
            else
            {
                GD.PrintErr($"Failed to save match log to {_logFilePath}");
            }
        }
    }

    private void CalculateFinalMetrics(string winner)
    {
        // Calculate additional metrics for the match summary
        int teamAUnitCount = GetTeamUnitCount(_teamA);
        int teamBUnitCount = GetTeamUnitCount(_teamB);

        float teamAHealthPercent = GetTeamHealthPercentage(_teamA);
        float teamBHealthPercent = GetTeamHealthPercentage(_teamB);

        int teamACampCount = GetAllCamps().Count(c => (int)c.TeamId == _teamA.TeamId);
        int teamBCampCount = GetAllCamps().Count(c => (int)c.TeamId == _teamB.TeamId);

        _matchLog.AppendLine("\n--- Final Match Metrics ---");
        _matchLog.AppendLine($"Winner: {winner}.");
        _matchLog.AppendLine($"Match Duration: {_elapsedTime:F1} seconds");
        _matchLog.AppendLine($"Team A Final Unit Count: {teamAUnitCount}");
        _matchLog.AppendLine($"Team B Final Unit Count: {teamBUnitCount}");
        _matchLog.AppendLine($"Team A Final Health %: {teamAHealthPercent:F1}");
        _matchLog.AppendLine($"Team B Final Health %: {teamBHealthPercent:F1}");
        _matchLog.AppendLine($"Team A Camp Count: {teamACampCount}");
        _matchLog.AppendLine($"Team B Camp Count: {teamBCampCount}");
        _matchLog.AppendLine($"Team A Combat Encounters: {_combatEncounters["TeamA"]}");
        _matchLog.AppendLine($"Team B Combat Encounters: {_combatEncounters["TeamB"]}");
        _matchLog.AppendLine($"Team A Scouted Cells: {GetScoutedCellsTeamA()} cells");
        _matchLog.AppendLine($"Team B Scouted Cells: {GetScoutedCellsTeamB()} cells");

        if (_reactionTimes["TeamA"] > 0)
        {
            _matchLog.AppendLine($"Team A Avg Reaction Time: {_reactionTimes["TeamA"]:F2} seconds");
        }

        if (_reactionTimes["TeamB"] > 0)
        {
            _matchLog.AppendLine($"Team B Avg Reaction Time: {_reactionTimes["TeamB"]:F2} seconds");
        }
    }

    private string GetTeamAIType(Team team)
    {
        // Check if team has BehaviorTree or BDI as children
        var btNode = team.GetNodeOrNull("BehaviorTreeAI");
        var bdiNode = team.GetNodeOrNull("BDIControllerAI");

        if (btNode != null)
        {
            return "BehaviorTree";
        }

        if (bdiNode != null)
        {
            return "BDI";
        }

        return "Unknown AI";
    }

    private void ConnectSignals()
    {
        // Connect signals to track metrics
        ConnectTeamSignals(_teamA, "TeamA");
        ConnectTeamSignals(_teamB, "TeamB");
    }

    private void DisconnectSignals()
    {
        // Disconnect signals
        DisconnectTeamSignals(_teamA);
        DisconnectTeamSignals(_teamB);
    }

    private void ConnectTeamSignals(Team team, string teamName)
    {
        // Get blackboard
        var blackboard = team.GetNode("Blackboard");
        if (blackboard != null)
        {
            // Create team-specific methods for signal handling
            if (teamName == "TeamA")
            {
                // Connect to team-specific methods
                blackboard.Connect("EnemyDetected", new Callable(this, "OnTeamAEnemyDetected"));
                blackboard.Connect("WeaponFired", new Callable(this, "OnTeamAWeaponFired"));
            }
            else if (teamName == "TeamB")
            {
                // Connect to team-specific methods
                blackboard.Connect("EnemyDetected", new Callable(this, "OnTeamBEnemyDetected"));
                blackboard.Connect("WeaponFired", new Callable(this, "OnTeamBWeaponFired"));
            }
        }
    }

    private void DisconnectTeamSignals(Team team)
    {
        var blackboard = team.GetNode("Blackboard");
        if (blackboard != null)
        {
            // Disconnect all signals regardless of which ones were connected
            if (team.TeamId == _teamA.TeamId)
            {
                blackboard.Disconnect("EnemyDetected", new Callable(this, "OnTeamAEnemyDetected"));
                blackboard.Disconnect("WeaponFired", new Callable(this, "OnTeamAWeaponFired"));
            }
            else if (team.TeamId == _teamB.TeamId)
            {
                blackboard.Disconnect("EnemyDetected", new Callable(this, "OnTeamBEnemyDetected"));
                blackboard.Disconnect("WeaponFired", new Callable(this, "OnTeamBWeaponFired"));
            }
        }
    }

    // Team-specific signal handlers
    private void OnTeamAEnemyDetected()
    {
        // Record when an enemy was spotted by Team A to measure reaction time
        _lastEnemySpottedTime["TeamA"] = _elapsedTime;
    }

    private void OnTeamBEnemyDetected()
    {
        // Record when an enemy was spotted by Team B to measure reaction time
        _lastEnemySpottedTime["TeamB"] = _elapsedTime;
    }

    private void OnTeamAWeaponFired()
    {
        // Count Team A combat encounters
        _combatEncounters["TeamA"]++;

        // Calculate reaction time if enemy was spotted first
        if (_lastEnemySpottedTime.ContainsKey("TeamA"))
        {
            float reactionTime = _elapsedTime - _lastEnemySpottedTime["TeamA"];

            // Only count reasonable reaction times (> 0.1s and < 15s)
            if (reactionTime > 0.1f && reactionTime < 15f)
            {
                // Update average reaction time
                if (_reactionTimes["TeamA"] == 0)
                {
                    _reactionTimes["TeamA"] = reactionTime;
                }
                else
                {
                    // Weighted average to prevent outliers from dominating
                    _reactionTimes["TeamA"] = _reactionTimes["TeamA"] * 0.7f + reactionTime * 0.3f;
                }
            }

            _lastActionTime["TeamA"] = _elapsedTime;
            _lastEnemySpottedTime.Remove("TeamA");  // Reset for next enemy
        }
    }

    private void OnTeamBWeaponFired()
    {
        // Count Team B combat encounters
        _combatEncounters["TeamB"]++;

        // Calculate reaction time if enemy was spotted first
        if (_lastEnemySpottedTime.ContainsKey("TeamB"))
        {
            float reactionTime = _elapsedTime - _lastEnemySpottedTime["TeamB"];

            // Only count reasonable reaction times (> 0.1s and < 15s)
            if (reactionTime > 0.1f && reactionTime < 15f)
            {
                // Update average reaction time
                if (_reactionTimes["TeamB"] == 0)
                {
                    _reactionTimes["TeamB"] = reactionTime;
                }
                else
                {
                    // Weighted average to prevent outliers from dominating
                    _reactionTimes["TeamB"] = _reactionTimes["TeamB"] * 0.7f + reactionTime * 0.3f;
                }
            }

            _lastActionTime["TeamB"] = _elapsedTime;
            _lastEnemySpottedTime.Remove("TeamB");  // Reset for next enemy
        }
    }
}
