using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IdlePet.Editor
{
    public static class GameConfigurationValidation
    {
        public const string ProductionAssetPath = "Assets/_Project/Content/Configs/Resources/IdlePet/GameConfig.asset";

        [MenuItem("Tools/Idle Pet/Validate Game Configuration")]
        public static void ValidateGameConfiguration()
        {
            var issues = CheckProduction();
            foreach (ConfigurationIssue issue in issues)
            {
                if (issue.Severity == ConfigurationSeverity.Error)
                {
                    Debug.LogError(issue.ToString());
                }
                else
                {
                    Debug.LogWarning(issue.ToString());
                }
            }

            int errors = issues.Count(issue => issue.Severity == ConfigurationSeverity.Error);
            Debug.Log($"Idle Pet configuration validation: {errors} errors, {issues.Count - errors} warnings. {ProductionAssetPath}");
        }

        public static IReadOnlyList<ConfigurationIssue> CheckProduction()
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            var scenes = profile != null ? profile.GetScenesForBuild() : EditorBuildSettings.globalScenes;
            string[] enabledScenes = scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            return CheckProduction(enabledScenes.Length == 0 ? new[] { SceneManager.GetActiveScene().path } : enabledScenes);
        }

        internal static IReadOnlyList<ConfigurationIssue> CheckProduction(string[] scenePaths)
        {
            string[] paths = AssetDatabase.GetAllAssetPaths();
            // Inspect native asset containers as well as indexed types to find embedded profiles.
            var configurations = paths.Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                .Concat(FindAssetPaths<GameConfigSO>())
                .Concat(FindAssetPaths<BalanceProfileSO>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Where(asset => asset is GameConfigSO || asset is BalanceProfileSO)
                .ToArray();
            var roots = configurations.OfType<GameConfigSO>().ToArray();
            var profiles = configurations.OfType<BalanceProfileSO>().ToArray();
            var issues = new List<ConfigurationIssue>(CheckProduction(roots, profiles, AssetDatabase.GetAssetPath));
            var playerRoots = PlayerConfigurationDependencies.FindRoots(paths, scenePaths, roots.Select(AssetDatabase.GetAssetPath));
            PlayerConfigurationDependencies.Check(profiles, playerRoots, issues);
            return issues.AsReadOnly();
        }

        internal static IReadOnlyList<ConfigurationIssue> CheckProduction(
            IReadOnlyList<GameConfigSO> roots, IReadOnlyList<BalanceProfileSO> profiles, Func<Object, string> assetPath)
        {
            var issues = new List<ConfigurationIssue>();
            GameConfigSO root = roots.Count == 1 ? roots[0] : null;
            CheckProfileInventory(profiles, root != null ? root.Profile : null, assetPath, issues);
            if (roots.Count == 0)
            {
                issues.Add(ProjectIssue(ConfigurationIssueCode.MissingRoot, ProductionAssetPath,
                    "Create the single Production game configuration at the required Resources path."));
            }
            else if (roots.Count > 1)
            {
                string paths = string.Join(", ", roots.Select(rootAsset => assetPath(rootAsset)));
                issues.Add(ProjectIssue(ConfigurationIssueCode.AmbiguousRoot, paths,
                    "Expected exactly one GameConfigSO in Assets."));
            }
            else
            {
                string path = assetPath(root);
                if (!string.Equals(path, ProductionAssetPath, StringComparison.Ordinal))
                {
                    issues.Add(ProjectIssue(ConfigurationIssueCode.IncorrectRootPath, path,
                        $"Move the root to {ProductionAssetPath} so its references are included in the Player."));
                }

                foreach (ConfigurationIssue issue in GameConfigMapper.Map(root, ConfigurationTarget.Release, assetPath).Issues)
                {
                    if (issue.Code == ConfigurationIssueCode.InvalidId || issue.Code == ConfigurationIssueCode.UnknownEnumValue)
                    {
                        if (issues.Any(existing => existing.Code == issue.Code &&
                            existing.Asset == issue.Asset && existing.Field == issue.Field))
                        {
                            continue;
                        }
                    }

                    issues.Add(issue);
                }
            }

            return issues.AsReadOnly();
        }

        private static void CheckProfileInventory(IReadOnlyList<BalanceProfileSO> profiles, BalanceProfileSO selected,
            Func<Object, string> assetPath, List<ConfigurationIssue> issues)
        {
            var pathsById = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (BalanceProfileSO profile in profiles)
            {
                string path = assetPath(profile);
                if (!ConfigurationId.IsValid(profile.Id))
                {
                    issues.Add(new ConfigurationIssue(ConfigurationIssueCode.InvalidId, ConfigurationSeverity.Error,
                        path, nameof(profile.Id), "A stable profile ID is required."));
                }
                else
                {
                    if (!pathsById.TryGetValue(profile.Id, out List<string> matchingPaths))
                    {
                        pathsById.Add(profile.Id, matchingPaths = new List<string>());
                    }

                    matchingPaths.Add(path);
                }

                if (!Enum.IsDefined(typeof(BalanceProfileKind), profile.Kind))
                {
                    issues.Add(new ConfigurationIssue(ConfigurationIssueCode.UnknownEnumValue, ConfigurationSeverity.Error,
                        path, nameof(profile.Kind), "Unknown balance profile."));
                }
                else if (profile.Kind != BalanceProfileKind.Production && profile != selected &&
                    !PlayerConfigurationDependencies.IsEditorOnly(path))
                {
                    issues.Add(new ConfigurationIssue(ConfigurationIssueCode.NonProductionProfile, ConfigurationSeverity.Error,
                        path, nameof(profile.Kind), "Keep non-Production profile assets in Editor-only folders or in transient tests."));
                }
            }

            foreach (var entry in pathsById)
            {
                if (entry.Value.Count > 1)
                {
                    entry.Value.Sort(StringComparer.Ordinal);
                    issues.Add(new ConfigurationIssue(ConfigurationIssueCode.DuplicateId, ConfigurationSeverity.Error,
                        entry.Value[0], nameof(BalanceProfileSO.Id), $"Duplicate balance profile ID '{entry.Key}'.", entry.Value.Skip(1)));
                }
            }
        }

        private static IEnumerable<string> FindAssetPaths<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}").Select(AssetDatabase.GUIDToAssetPath);
        }

        private static ConfigurationIssue ProjectIssue(ConfigurationIssueCode code, string path, string message)
        {
            return new ConfigurationIssue(code, ConfigurationSeverity.Error, path, nameof(GameConfigSO), message);
        }
    }
}
