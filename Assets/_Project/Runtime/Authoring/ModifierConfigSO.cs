using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdlePet.Authoring
{
    [CreateAssetMenu(menuName = "Idle Pet/Configuration/Modifiers")]
    public sealed class ModifierConfigSO : ScriptableObject
    {
        [Tooltip("Optional multipliers on the base rates of stored needs.")]
        [SerializeField] private RateModifierAuthoringData[] _rateMultipliers = Array.Empty<RateModifierAuthoringData>();

        public IReadOnlyList<RateModifierAuthoringData> RateMultipliers => _rateMultipliers;
    }
}
