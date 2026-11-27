using System;
using System.Collections.Generic;
using System.Globalization;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdlePet.Editor.BalanceLab
{
    internal static class LabProfiles
    {
        internal static ConfigurationMappingResult Map(GameConfigSO root, BalanceProfileKind? generated = null)
        {
            if (!generated.HasValue || generated == BalanceProfileKind.Production || root == null)
            {
                var target = root != null && root.Profile != null && root.Profile.Kind != BalanceProfileKind.Production &&
                    !generated.HasValue ? ConfigurationTarget.Testing : ConfigurationTarget.Release;
                return GameConfigMapper.Map(root, target, AssetDatabase.GetAssetPath);
            }

            var production = GameConfigMapper.Map(root, ConfigurationTarget.Release, AssetDatabase.GetAssetPath);
            if (!production.Succeeded)
            {
                return production;
            }

            bool stress = generated == BalanceProfileKind.StressTesting;
            var temporary = new List<Object>();
            try
            {
                var testRoot = Clone(root, temporary);
                var profile = Clone(root.Profile, temporary);
                var needs = Clone(root.Profile.Needs, temporary);
                var time = Clone(root.Profile.Time, temporary);
                var simulation = Clone(root.Profile.Simulation, temporary);
                var modifiers = Clone(root.Profile.Modifiers, temporary);
                Edit(testRoot, p => p.FindProperty("_profile").objectReferenceValue = profile);
                Edit(profile, p =>
                {
                    p.FindProperty("_id").stringValue = stress ? "lab.stress" : "lab.fast";
                    p.FindProperty("_kind").intValue = (int)generated.Value;
                    p.FindProperty("_simulation").objectReferenceValue = simulation;
                    p.FindProperty("_time").objectReferenceValue = time;
                    p.FindProperty("_needs").objectReferenceValue = needs;
                    p.FindProperty("_modifiers").objectReferenceValue = modifiers;
                });
                Edit(time, p =>
                {
                    p.FindProperty("_visualDayMinutes").longValue = stress ? 1 : 5;
                    p.FindProperty("_biologicalDayHours").longValue = stress ? 48 : 12;
                });
                Edit(simulation, p => p.FindProperty("_maxEventsPerTimestamp").intValue = stress ? 256 : 8);
                Edit(needs, p => SetNeeds(p.FindProperty("_entries"), stress));
                Edit(modifiers, p => SetModifiers(p.FindProperty("_rateMultipliers"), stress));
                return GameConfigMapper.Map(testRoot, ConfigurationTarget.Testing);
            }
            finally
            {
                for (int i = temporary.Count - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(temporary[i]);
                }
            }
        }

        private static void SetNeeds(SerializedProperty entries, bool stress)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var id = (NeedId)entry.FindPropertyRelative("_id").intValue;
                if (id == NeedId.Satiety || id == NeedId.Energy)
                {
                    entry.FindPropertyRelative("_initialValue").stringValue = stress ? "100" : "60";
                    entry.FindPropertyRelative("_baseRatePerHour").stringValue = stress ? "-8000000000" : "-3600";
                    var thresholds = entry.FindPropertyRelative("_thresholds");
                    thresholds.arraySize = 2;
                    thresholds.GetArrayElementAtIndex(0).stringValue = "10";
                    thresholds.GetArrayElementAtIndex(1).stringValue = "25";
                }
            }
        }

        private static void SetModifiers(SerializedProperty entries, bool stress)
        {
            entries.arraySize = stress ? 32 : 2;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_id").stringValue = "lab.mod." + i.ToString("D2", CultureInfo.InvariantCulture);
                entry.FindPropertyRelative("_target").intValue = (int)(i % 2 == 0 ? NeedId.Satiety : NeedId.Energy);
                entry.FindPropertyRelative("_coefficient").stringValue = stress ? "1.125" : "2";
            }
        }

        private static T Clone<T>(T source, ICollection<Object> temporary) where T : ScriptableObject
        {
            var clone = Object.Instantiate(source);
            temporary.Add(clone);
            clone.hideFlags = HideFlags.HideAndDontSave;
            return clone;
        }

        private static void Edit(Object target, Action<SerializedObject> change)
        {
            using var serialized = new SerializedObject(target);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
