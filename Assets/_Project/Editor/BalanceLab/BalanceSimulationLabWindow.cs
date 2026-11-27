using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdlePet.Authoring;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Simulation.Engine;
using UnityEditor;
using UnityEngine;

namespace IdlePet.Editor.BalanceLab
{
    public sealed class BalanceSimulationLabWindow : EditorWindow
    {
        private static readonly string[] Durations = { "1 min", "1 h", "8 h", "24 h", "7 d", "365 d", "Custom" };
        private static readonly string[] Milliseconds = { "60000", "3600000", "28800000", "86400000", "604800000", "31536000000" };
        private const string MenuPath = "Tools/Idle Pet/Balance Simulation Lab";
        private const decimal NeedUnitsDivisor = (decimal)NeedRemainder.Denominator * NeedValue.MilliUnitsPerUnit;
        private GameConfigSO _root;
        private LabScenario _scenario;
        private LabMode _mode = LabMode.Compare;
        private string _seed = "42";
        private string _start = "0";
        private string _duration = "86400000";
        private int _preset = 3;
        private int _segments = 31;
        private bool _activateModifiers;
        private bool _showDetails;
        private Vector2 _scroll;
        private LabResult _result;
        private string _error;
        private string _details;
        private string _samples;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<BalanceSimulationLabWindow>("Balance Simulation Lab");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
        }

        private void OnGUI()
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            var scenario = (LabScenario)EditorGUILayout.EnumPopup("Scenario", _scenario);
            if (scenario != _scenario)
            {
                if (scenario == LabScenario.PartitionParity || scenario == LabScenario.ForecastParity)
                {
                    _mode = LabMode.Compare;
                }
                _scenario = scenario;
            }

            _root = (GameConfigSO)EditorGUILayout.ObjectField("Game configuration", _root, typeof(GameConfigSO), false);
            _mode = (LabMode)EditorGUILayout.EnumPopup("Mode", _mode);
            _seed = EditorGUILayout.TextField("World seed", _seed);
            _start = EditorGUILayout.TextField("Origin (ms)", _start);
            int preset = EditorGUILayout.Popup("Duration", _preset, Durations);
            if (preset != _preset)
            {
                _preset = preset;
                if (preset < Milliseconds.Length)
                {
                    _duration = Milliseconds[preset];
                }
            }
            if (_preset == Durations.Length - 1)
            {
                _duration = EditorGUILayout.TextField("Duration (ms)", _duration);
            }
            _segments = EditorGUILayout.IntField("Segments", _segments);
            _activateModifiers = EditorGUILayout.Toggle("Activate test modifiers", _activateModifiers);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run"))
                {
                    Run();
                }
                if (GUILayout.Button("Reset Result"))
                {
                    ClearResult();
                }
                using (new EditorGUI.DisabledScope(_result == null))
                {
                    if (GUILayout.Button("Export XML"))
                    {
                        var path = EditorUtility.SaveFilePanel("Export simulation diagnostics", "", "balance-simulation.xml", "xml");
                        if (path.Length != 0)
                        {
                            try { ExportTo(path); }
                            catch (IOException error) { _error = error.Message; }
                            catch (UnauthorizedAccessException error) { _error = error.Message; }
                        }
                    }
                }
            }
            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
            if (_result == null)
            {
                return;
            }
            if (!_result.Mapping.Succeeded)
            {
                foreach (var issue in _result.Mapping.Issues)
                {
                    EditorGUILayout.HelpBox(issue.ToString(), MessageType.Error);
                }
                return;
            }

            EditorGUILayout.Space();
            DrawSummary();
            if (GUILayout.Button("Sample need values"))
            {
                _samples = SampleNeeds();
            }
            if (_samples != null)
            {
                EditorGUILayout.HelpBox(_samples, MessageType.None);
            }
            _showDetails = EditorGUILayout.Foldout(_showDetails, "RNG, queue, trace and diagnostics (XML)", true);
            if (_showDetails)
            {
                _details ??= LabReport.Export(_result).ToString();
                EditorGUILayout.SelectableLabel(_details, EditorStyles.textArea, GUILayout.MinHeight(260));
            }
        }

        private void DrawSummary()
        {
            var config = _result.Definition;
            var request = _result.Request;
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Profile", $"{config.ProfileId} ({config.ProfileKind})");
            EditorGUILayout.LabelField("Versions", $"Rules {config.Needs.RulesVersion} · Balance {config.Needs.BalanceVersion} · Content {config.Needs.ContentVersion} · RNG {_result.Source.Rng.RngAlgorithmVersion}");
            EditorGUILayout.LabelField("Seed", request.Seed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Origin → target (ms)", $"{request.Origin.Milliseconds} → {request.Target.Milliseconds}");
            if (request.Mode == LabMode.Compare)
            {
                EditorGUILayout.HelpBox(_result.Difference ?? "All four branches match in state, trace and diagnosis.",
                    _result.Difference == null ? MessageType.Info : MessageType.Error);
            }
            for (int i = 0; i < _result.Projections.Count; i++)
            {
                var projection = _result.Projections[i];
                EditorGUILayout.LabelField(_result.Strategy(i),
                    $"{projection.Status} · {projection.Trace.Count} events · {projection.Transactions} tx · {projection.ElapsedMs:F3} ms");
                if (projection.Diagnostic != null)
                {
                    EditorGUILayout.HelpBox(projection.Diagnostic.Reason, MessageType.Error);
                }
            }
            var state = _result.Projections[0].State;
            if (state == null)
            {
                return;
            }
            EditorGUILayout.LabelField("Reached (ms)", state.LogicalTimeMilliseconds.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Queue / Seq / Budget",
                $"{state.EventQueue.Events.Count} / {state.EventQueue.NextSequence} / {state.ProcessedEventsAtCursor}");
            EditorGUILayout.LabelField("Terminal time", state.ActivePet?.TerminatedAt?.Milliseconds.ToString(CultureInfo.InvariantCulture) ?? "—");
            if (state.ActivePet == null)
            {
                return;
            }
            foreach (var need in state.ActivePet.Needs.Values)
            {
                var before = _result.Source.ActivePet.Needs[need.Key];
                decimal delta = ((decimal)need.Value.ExactNumerator - before.ExactNumerator) / NeedUnitsDivisor;
                EditorGUILayout.LabelField(need.Key.ToString(),
                    $"{before.ExactNumerator / NeedUnitsDivisor:0.######} → {need.Value.ExactNumerator / NeedUnitsDivisor:0.######} ({delta:+0.######;-0.######;0}), remainder {need.Value.Remainder.Numerator}");
            }
        }

        private void ClearResult()
        {
            _result = null;
            _details = null;
            _samples = null;
            _error = null;
        }

        private void Run()
        {
            ClearResult();
            try
            {
                _result = LabRunner.Run(LabRequest.Parse(_root, _scenario, _mode, _seed, _start, _duration, _segments, _activateModifiers));
            }
            catch (ArgumentException error) { _error = error.Message; }
            catch (OverflowException) { _error = "The target is outside the logical time range."; }
            Repaint();
        }

        private string SampleNeeds()
        {
            var source = _result.Source;
            long span = _result.Request.Target.Milliseconds - source.LogicalTimeMilliseconds;
            var engine = new SimulationEngine(_result.Definition);
            var builder = new StringBuilder();
            foreach (int hours in new[] { 0, 1, 4, 8, 12, 24 })
            {
                long elapsed = hours * 3_600_000L;
                if (elapsed > span)
                {
                    break;
                }
                var forecast = LabRunner.Project(engine, source, new[] { source.LogicalTimeMilliseconds + elapsed }, forecast: true);
                var values = forecast.State?.ActivePet?.Needs.Values
                    .Select(need => $"{need.Key} {need.Value.ExactNumerator / NeedUnitsDivisor:0.###}");
                builder.Append(hours).Append(" h · ").AppendLine(values == null ? forecast.Status.ToString() : string.Join(" · ", values));
            }
            return builder.ToString();
        }

        private void ExportTo(string path) => LabReport.Export(_result).Save(path);
    }
}
