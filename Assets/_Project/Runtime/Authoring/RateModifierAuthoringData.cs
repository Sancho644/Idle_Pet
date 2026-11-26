using System;
using IdlePet.Domain.Pet.Needs;
using UnityEngine;

namespace IdlePet.Authoring
{
    [Serializable]
    public sealed class RateModifierAuthoringData
    {
        [Tooltip("Stable modifier ID. Preserve it when moving or renaming the asset.")]
        [SerializeField] private string _id;

        [SerializeField] private NeedId _target;

        [Tooltip("Non-negative rate multiplier. Use a decimal point and at most six fractional digits.")]
        [SerializeField] private string _coefficient;

        public string Id => _id;

        public NeedId Target => _target;

        public string Coefficient => _coefficient;
    }
}
