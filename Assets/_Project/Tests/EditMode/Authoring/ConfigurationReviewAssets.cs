using System;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Domain.Definitions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdlePet.Tests.EditMode.Authoring
{
    internal sealed class ConfigurationReviewAssets : IDisposable
    {
        private const string EditorDefaults = "Assets/Editor Default Resources";
        private readonly string _name = "IdlePetConfigReview_" + Guid.NewGuid().ToString("N");
        private bool _createdEditorDefaults;
        private bool _usedEditorDefaults;

        internal ConfigurationReviewAssets()
        {
            EnsureFolder(Root);
        }

        internal string Root => "Assets/" + _name;

        internal BalanceProfileSO CreateProfile(string relativePath, string id, BalanceProfileKind kind)
        {
            var profile = NewProfile(id, kind);
            AssetDatabase.CreateAsset(profile, PreparePath(relativePath));
            return profile;
        }

        internal BalanceProfileSO CreateEditorDefaultProfile(string id, BalanceProfileKind kind)
        {
            if (!AssetDatabase.IsValidFolder(EditorDefaults))
            {
                EnsureFolder(EditorDefaults);
                _createdEditorDefaults = true;
            }

            _usedEditorDefaults = true;
            string parent = EditorDefaults + "/" + _name;
            EnsureFolder(parent);
            var profile = NewProfile(id, kind);
            AssetDatabase.CreateAsset(profile, parent + "/Profile.asset");
            return profile;
        }

        internal ProfileReferenceAsset CreateReference(string relativePath, Object reference = null)
        {
            var asset = ScriptableObject.CreateInstance<ProfileReferenceAsset>();
            ConfigurationFixture.Edit(asset, s => s.FindProperty("_reference").objectReferenceValue = reference);
            AssetDatabase.CreateAsset(asset, PreparePath(relativePath));
            return asset;
        }

        internal BalanceProfileSO AddProfileToAsset(Object container, string id, BalanceProfileKind kind)
        {
            var profile = NewProfile(id, kind);
            profile.name = id;
            string path = AssetDatabase.GetAssetPath(container);
            AssetDatabase.AddObjectToAsset(profile, container);
            EditorUtility.SetDirty(container);
            AssetDatabase.SaveAssetIfDirty(container);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<BalanceProfileSO>().Single();
        }

        private string PreparePath(string relativePath)
        {
            string path = Root + "/" + relativePath;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            return path;
        }

        private static BalanceProfileSO NewProfile(string id, BalanceProfileKind kind)
        {
            var profile = ScriptableObject.CreateInstance<BalanceProfileSO>();
            ConfigurationFixture.Edit(profile, s =>
            {
                s.FindProperty("_kind").intValue = (int)kind;
                s.FindProperty("_id").stringValue = id;
                s.FindProperty("_balanceVersion").stringValue = "0.1";
            });
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int index = path.LastIndexOf('/');
            string parent = path.Substring(0, index);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(index + 1))))
            {
                throw new InvalidOperationException("Cannot create test asset folder " + path);
            }
        }

        public void Dispose()
        {
            DeleteFolder(Root);
            if (_usedEditorDefaults)
            {
                DeleteFolder(EditorDefaults + "/" + _name);
            }

            if (_createdEditorDefaults && !AssetDatabase.GetAllAssetPaths()
                .Any(path => path.StartsWith(EditorDefaults + "/", StringComparison.OrdinalIgnoreCase)))
            {
                DeleteFolder(EditorDefaults);
            }
        }

        private static void DeleteFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path) && !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("Cannot clean up test asset folder " + path);
            }
        }
    }
}
