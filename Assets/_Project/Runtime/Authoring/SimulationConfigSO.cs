using UnityEngine;

namespace IdlePet.Authoring
{
    [CreateAssetMenu(menuName = "Idle Pet/Configuration/Simulation")]
    public sealed class SimulationConfigSO : ScriptableObject
    {
        [Tooltip("Maximum events at one logical timestamp. This does not limit offline progress.")]
        [SerializeField] private int _maxEventsPerTimestamp;

        public int MaxEventsPerTimestamp => _maxEventsPerTimestamp;
    }
}
