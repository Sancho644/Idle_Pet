using System;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdlePet.Tests.EditMode.Authoring
{
    internal sealed class ConfigurationFixture : IDisposable
    {
        internal ConfigurationFixture(BalanceProfileKind profileKind = BalanceProfileKind.Production)
        {
            Simulation = ScriptableObject.CreateInstance<SimulationConfigSO>();
            Time = ScriptableObject.CreateInstance<TimeConfigSO>();
            Needs = ScriptableObject.CreateInstance<NeedsConfigSO>();
            Modifiers = ScriptableObject.CreateInstance<ModifierConfigSO>();
            Profile = ScriptableObject.CreateInstance<BalanceProfileSO>();
            Root = ScriptableObject.CreateInstance<GameConfigSO>();
            Assets = new ScriptableObject[] { Root, Profile, Simulation, Time, Needs, Modifiers };
            foreach (ScriptableObject asset in Assets)
            {
                asset.name = asset.GetType().Name + " Test Fixture";
            }

            Edit(Simulation, s => s.FindProperty("_maxEventsPerTimestamp").intValue = 31);
            Edit(Time, s =>
            {
                s.FindProperty("_visualDayMinutes").longValue = 13;
                s.FindProperty("_biologicalDayHours").longValue = 24;
                s.FindProperty("_minNightHours").longValue = 6;
                s.FindProperty("_maxNightHours").longValue = 10;
                s.FindProperty("_scheduleChangeIntervalDays").longValue = 7;
            });
            Edit(Needs, s =>
            {
                var entries = s.FindProperty("_entries");
                NeedId[] ids = ((NeedId[])Enum.GetValues(typeof(NeedId))).Where(id => id != NeedId.Comfort).ToArray();
                entries.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("_id").intValue = (int)ids[i];
                    entry.FindPropertyRelative("_minimum").stringValue = "0";
                    entry.FindPropertyRelative("_maximum").stringValue = "100";
                    entry.FindPropertyRelative("_initialValue").stringValue = "12.345";
                    entry.FindPropertyRelative("_baseRatePerHour").stringValue = "-1.5";
                }
            });
            SetModifier(0, "test.satiety", NeedId.Satiety, "1.25");
            SetModifier(1, "test.energy", NeedId.Energy, "0.000001");
            Edit(Profile, s =>
            {
                s.FindProperty("_id").stringValue = "test." + profileKind;
                s.FindProperty("_kind").intValue = (int)profileKind;
                s.FindProperty("_balanceVersion").stringValue = "2.3";
                s.FindProperty("_simulation").objectReferenceValue = Simulation;
                s.FindProperty("_time").objectReferenceValue = Time;
                s.FindProperty("_needs").objectReferenceValue = Needs;
                s.FindProperty("_modifiers").objectReferenceValue = Modifiers;
            });
            Edit(Root, s =>
            {
                s.FindProperty("_profile").objectReferenceValue = Profile;
                s.FindProperty("_rulesVersion").stringValue = "1.2";
                s.FindProperty("_contentVersion").stringValue = "3.4.5";
            });
        }

        internal SimulationConfigSO Simulation { get; }

        internal TimeConfigSO Time { get; }

        internal NeedsConfigSO Needs { get; }

        internal ModifierConfigSO Modifiers { get; }

        internal BalanceProfileSO Profile { get; }

        internal GameConfigSO Root { get; }

        internal ScriptableObject[] Assets { get; }

        internal void ChangeNeed(int index, Action<SerializedProperty> set)
        {
            Edit(Needs, s => set(s.FindProperty("_entries").GetArrayElementAtIndex(index)));
        }

        internal void SetThresholds(params string[] thresholds)
        {
            ChangeNeed(0, entry =>
            {
                var field = entry.FindPropertyRelative("_thresholds");
                field.arraySize = thresholds.Length;
                for (int i = 0; i < thresholds.Length; i++)
                {
                    field.GetArrayElementAtIndex(i).stringValue = thresholds[i];
                }
            });
        }

        internal void SetModifier(int index, string id, NeedId target, string coefficient)
        {
            Edit(Modifiers, s =>
            {
                var entries = s.FindProperty("_rateMultipliers");
                entries.arraySize = Math.Max(entries.arraySize, index + 1);
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_id").stringValue = id;
                entry.FindPropertyRelative("_target").intValue = (int)target;
                entry.FindPropertyRelative("_coefficient").stringValue = coefficient;
            });
        }

        internal static void Edit(ScriptableObject asset, Action<SerializedObject> set)
        {
            using var serialized = new SerializedObject(asset);
            set(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Dispose()
        {
            foreach (ScriptableObject asset in Assets)
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
