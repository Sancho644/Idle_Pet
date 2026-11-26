using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdlePet.Authoring
{
    [CreateAssetMenu(menuName = "Idle Pet/Configuration/Needs")]
    public sealed class NeedsConfigSO : ScriptableObject
    {
        [Tooltip("Eight stored needs. Comfort is calculated separately.")]
        [SerializeField] private NeedAuthoringData[] _entries = Array.Empty<NeedAuthoringData>();

        public IReadOnlyList<NeedAuthoringData> Entries => _entries;
    }
}
