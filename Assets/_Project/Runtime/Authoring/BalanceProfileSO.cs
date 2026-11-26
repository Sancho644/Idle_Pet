using IdlePet.Domain.Definitions;
using UnityEngine;

namespace IdlePet.Authoring
{
    [CreateAssetMenu(menuName = "Idle Pet/Configuration/Balance Profile")]
    public sealed class BalanceProfileSO : ScriptableObject
    {
        [Tooltip("Stable profile ID. Preserve it when renaming the asset.")]
        [SerializeField] private string _id;

        [SerializeField] private BalanceProfileKind _kind;

        [Tooltip("Balance version in major.minor or major.minor.patch format.")]
        [SerializeField] private string _balanceVersion;

        [Header("Required configurations")]
        [SerializeField] private SimulationConfigSO _simulation;
        [SerializeField] private TimeConfigSO _time;
        [SerializeField] private NeedsConfigSO _needs;
        [SerializeField] private ModifierConfigSO _modifiers;

        public string Id => _id;

        public BalanceProfileKind Kind => _kind;

        public string BalanceVersion => _balanceVersion;

        public SimulationConfigSO Simulation => _simulation;

        public TimeConfigSO Time => _time;

        public NeedsConfigSO Needs => _needs;

        public ModifierConfigSO Modifiers => _modifiers;
    }
}
