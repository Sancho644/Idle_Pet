using System;
using System.Globalization;
using System.IO;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using IdlePet.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class ConfigurationReviewFixTests
    {
        [TestCase(BalanceProfileKind.Production, BalanceProfileKind.Production)]
        [TestCase(BalanceProfileKind.Production, BalanceProfileKind.FastTesting)]
        [TestCase(BalanceProfileKind.FastTesting, BalanceProfileKind.StressTesting)]
        public void DuplicateProfileIdsReportBothRealAssetPaths(BalanceProfileKind first, BalanceProfileKind second)
        {
            using var assets = new ConfigurationReviewAssets();
            var left = assets.CreateProfile("Editor/Left.asset", "review.same", first);
            var right = assets.CreateProfile("Editor/Right.asset", "review.same", second);

            var issue = GameConfigurationValidation.CheckProduction().Single(item => item.Code == ConfigurationIssueCode.DuplicateId);

            Assert.That(issue.Severity, Is.EqualTo(ConfigurationSeverity.Error));
            Assert.That(issue.Field, Is.EqualTo(nameof(BalanceProfileSO.Id)));
            Assert.That(issue.Asset, Is.EqualTo(AssetDatabase.GetAssetPath(left)));
            Assert.That(issue.RelatedAssets, Is.EqualTo(new[] { AssetDatabase.GetAssetPath(right) }));
            Assert.That(issue.Message, Does.Contain("review.same"));
        }

        [Test]
        public void SelectedProductionIdIsCheckedAgainstAnAdditionalProfile()
        {
            using var assets = new ConfigurationReviewAssets();
            var root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
            var additional = assets.CreateProfile("Editor/Additional.asset", root.Profile.Id, BalanceProfileKind.FastTesting);

            var issue = GameConfigurationValidation.CheckProduction().Single(item => item.Code == ConfigurationIssueCode.DuplicateId);

            Assert.That(issue.RelatedAssets.Prepend(issue.Asset), Is.EquivalentTo(new[]
            {
                AssetDatabase.GetAssetPath(root.Profile), AssetDatabase.GetAssetPath(additional)
            }));
        }

        [Test]
        public void EmptyIdOnAdditionalProductionProfileIsRejected()
        {
            using var assets = new ConfigurationReviewAssets();
            var profile = assets.CreateProfile("Editor/InvalidId.asset", "", BalanceProfileKind.Production);

            var issue = GameConfigurationValidation.CheckProduction().Single(item => item.Code == ConfigurationIssueCode.InvalidId);

            Assert.That(issue.Asset, Is.EqualTo(AssetDatabase.GetAssetPath(profile)));
            Assert.That(issue.Field, Is.EqualTo(nameof(BalanceProfileSO.Id)));
        }

        [Test]
        public void UnknownKindOnAdditionalProfileIsRejected()
        {
            using var assets = new ConfigurationReviewAssets();
            var profile = assets.CreateProfile("Editor/UnknownKind.asset", "review.unknown", (BalanceProfileKind)99);

            var issue = GameConfigurationValidation.CheckProduction().Single(item => item.Code == ConfigurationIssueCode.UnknownEnumValue);

            Assert.That(issue.Asset, Is.EqualTo(AssetDatabase.GetAssetPath(profile)));
            Assert.That(issue.Field, Is.EqualTo(nameof(BalanceProfileSO.Kind)));
        }

        [Test]
        public void DistinctValidProfileIdsAreAccepted()
        {
            using var assets = new ConfigurationReviewAssets();
            assets.CreateProfile("Editor/Prod.asset", "review.prod", BalanceProfileKind.Production);
            assets.CreateProfile("Editor/Fast.asset", "review.fast", BalanceProfileKind.FastTesting);
            assets.CreateProfile("Editor/Stress.asset", "review.stress", BalanceProfileKind.StressTesting);

            Assert.That(GameConfigurationValidation.CheckProduction(), Is.Empty);
        }

        [Test]
        public void ProfileIdUniquenessIsOrdinalAndCaseSensitive()
        {
            using var assets = new ConfigurationReviewAssets();
            assets.CreateProfile("Editor/Upper.asset", "review.I", BalanceProfileKind.Production);
            assets.CreateProfile("Editor/Lower.asset", "review.i", BalanceProfileKind.StressTesting);
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(GameConfigurationValidation.CheckProduction(), Is.Empty);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestCase("Runtime/Testing.asset", true)]
        [TestCase("Editor/Resources/Testing.asset", false)]
        [TestCase("Nested/Editor Default Resources/Testing.asset", true)]
        public void TestProfilePlacementUsesRealAssetPaths(string relativePath, bool reject)
        {
            using var assets = new ConfigurationReviewAssets();
            var profile = assets.CreateProfile(relativePath, "review.placement", BalanceProfileKind.FastTesting);

            var issues = GameConfigurationValidation.CheckProduction();

            if (reject)
            {
                Assert.That(issues.Single().Code, Is.EqualTo(ConfigurationIssueCode.NonProductionProfile));
                Assert.That(issues.Single().Asset, Is.EqualTo(AssetDatabase.GetAssetPath(profile)));
            }
            else
            {
                Assert.That(issues, Is.Empty);
            }
        }

        [Test]
        public void RootEditorDefaultResourcesIsAllowedWhenUnreferenced()
        {
            using var assets = new ConfigurationReviewAssets();
            var profile = assets.CreateEditorDefaultProfile("review.defaults", BalanceProfileKind.StressTesting);

            Assert.That(AssetDatabase.GetAssetPath(profile), Does.StartWith("Assets/Editor Default Resources/"));
            Assert.That(GameConfigurationValidation.CheckProduction(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RuntimeResourceCannotReachEditorTestProfilesAndValidationDoesNotMutateAssets(bool embedProfile)
        {
            using var assets = new ConfigurationReviewAssets();
            BalanceProfileSO profile;
            if (embedProfile)
            {
                var container = assets.CreateReference("Editor/Embedded.asset");
                profile = assets.AddProfileToAsset(container, "review.embedded", BalanceProfileKind.StressTesting);
            }
            else
            {
                profile = assets.CreateEditorDefaultProfile("review.reachable", BalanceProfileKind.FastTesting);
            }

            var bridge = assets.CreateReference("Bridge.asset", profile);
            var resource = assets.CreateReference("Resources/Runtime.asset", bridge);
            string profilePath = AssetDatabase.GetAssetPath(profile);
            string resourcePath = AssetDatabase.GetAssetPath(resource);
            string[] paths = { profilePath, AssetDatabase.GetAssetPath(bridge), resourcePath };
            byte[][] before = paths.Select(File.ReadAllBytes).ToArray();

            Assert.That(resource.Reference, Is.EqualTo(bridge));
            Assert.That(AssetDatabase.GetDependencies(resourcePath, true), Has.Member(profilePath));
            var issue = GameConfigurationValidation.CheckProduction().Single(item =>
                item.Code == ConfigurationIssueCode.TestProfileInPlayerDependencies);

            Assert.That(issue.Asset, Is.EqualTo(profilePath));
            Assert.That(issue.RelatedAssets, Does.Contain(resourcePath));
            Assert.That(paths.Select(File.ReadAllBytes).ToArray(), Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(profile) || EditorUtility.IsDirty(bridge) || EditorUtility.IsDirty(resource), Is.False);
        }

        [Test]
        public void SceneInBuildOptionsCannotReachEditorTestProfileThroughAnAsset()
        {
            using var assets = new ConfigurationReviewAssets();
            var profile = assets.CreateProfile("Editor/SceneProfile.asset", "review.scene", BalanceProfileKind.StressTesting);
            var bridge = assets.CreateReference("SceneBridge.asset", profile);
            string scenePath = assets.Root + "/Fixture.unity";
            Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", scenePath), Is.True);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var director = scene.GetRootGameObjects().First().AddComponent<PlayableDirector>();
                director.SetReferenceValue(new PropertyName("idle-pet-review-profile"), bridge);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(AssetDatabase.GetDependencies(scenePath, true), Has.Member(AssetDatabase.GetAssetPath(profile)));

                var validator = new ConfigurationBuildValidator();
                Assert.That(GameConfigurationValidation.CheckProduction(Array.Empty<string>()), Is.Empty);
                var error = Assert.Throws<BuildFailedException>(() =>
                    validator.ValidateBuild(new BuildPlayerOptions { scenes = new[] { scenePath } }));
                Assert.That(error.Message, Does.Contain(scenePath).And.Contain(AssetDatabase.GetAssetPath(profile))
                    .And.Contain(nameof(ConfigurationIssueCode.TestProfileInPlayerDependencies)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded && SceneManager.GetActiveScene() != previous)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }
    }
}
