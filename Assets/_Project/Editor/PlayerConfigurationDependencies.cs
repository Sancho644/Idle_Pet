using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IdlePet.Editor
{
    internal static class PlayerConfigurationDependencies
    {
        internal static string[] FindRoots(IEnumerable<string> assetPaths, IEnumerable<string> scenePaths,
            IEnumerable<string> productionPaths)
        {
            var roots = new HashSet<string>(productionPaths, StringComparer.Ordinal);
            foreach (string path in scenePaths)
            {
                roots.Add(path);
            }

            foreach (string path in assetPaths)
            {
                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !IsEditorOnly(path) && !AssetDatabase.IsValidFolder(path))
                {
                    roots.Add(path);
                }
            }

            foreach (Object asset in PlayerSettings.GetPreloadedAssets())
            {
                roots.Add(AssetDatabase.GetAssetPath(asset));
            }

            roots.Add(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                roots.Add(AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i)));
            }

            return roots.Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        internal static void Check(IEnumerable<BalanceProfileSO> profiles, string[] roots, List<ConfigurationIssue> issues)
        {
            var playerDependencies = new HashSet<string>(AssetDatabase.GetDependencies(roots, true), StringComparer.Ordinal);
            var reachableProfiles = profiles
                .Where(profile => profile.Kind == BalanceProfileKind.FastTesting || profile.Kind == BalanceProfileKind.StressTesting)
                .Select(profile => (Profile: profile, Path: AssetDatabase.GetAssetPath(profile)))
                .Where(entry => playerDependencies.Contains(entry.Path))
                .ToArray();
            if (reachableProfiles.Length == 0)
            {
                return;
            }

            var dependenciesByRoot = roots.ToDictionary(path => path,
                path => new HashSet<string>(AssetDatabase.GetDependencies(path, true), StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var entry in reachableProfiles)
            {
                var referencingPaths = roots.Where(path => dependenciesByRoot[path].Contains(entry.Path));
                issues.Add(new ConfigurationIssue(ConfigurationIssueCode.TestProfileInPlayerDependencies,
                    ConfigurationSeverity.Error, entry.Path, nameof(BalanceProfileSO.Kind),
                    $"The Player dependency graph reaches {entry.Profile.Kind} profile '{entry.Profile.Id}'.", referencingPaths));
            }
        }

        internal static bool IsEditorOnly(string path)
        {
            if (path.StartsWith("Assets/Editor Default Resources/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] segments = path.Split('/');
            for (int i = 1; i < segments.Length - 1; i++)
            {
                if (string.Equals(segments[i], "Editor", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
