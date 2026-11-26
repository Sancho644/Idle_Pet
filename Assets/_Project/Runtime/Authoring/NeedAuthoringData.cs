using System;
using System.Collections.Generic;
using IdlePet.Domain.Pet.Needs;
using UnityEngine;

namespace IdlePet.Authoring
{
    [Serializable]
    public sealed class NeedAuthoringData
    {
        [SerializeField] private NeedId _id;

        [Tooltip("Need units. Use a decimal point and at most three fractional digits.")]
        [SerializeField] private string _minimum;

        [Tooltip("Need units. Use a decimal point and at most three fractional digits.")]
        [SerializeField] private string _maximum;

        [Tooltip("Need units. Use a decimal point and at most three fractional digits.")]
        [SerializeField] private string _initialValue;

        [Tooltip("Signed need units per real hour. Use a decimal point and at most three fractional digits.")]
        [SerializeField] private string _baseRatePerHour;

        [Tooltip("Optional thresholds in need units. Use a decimal point and at most three fractional digits.")]
        [SerializeField] private string[] _thresholds = Array.Empty<string>();

        public NeedId Id => _id;

        public string Minimum => _minimum;

        public string Maximum => _maximum;

        public string InitialValue => _initialValue;

        public string BaseRatePerHour => _baseRatePerHour;

        public IReadOnlyList<string> Thresholds => _thresholds;
    }
}
