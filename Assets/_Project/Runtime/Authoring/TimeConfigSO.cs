using UnityEngine;

namespace IdlePet.Authoring
{
    [CreateAssetMenu(menuName = "Idle Pet/Configuration/Time")]
    public sealed class TimeConfigSO : ScriptableObject
    {
        [Header("Real time")]
        [SerializeField] private long _visualDayMinutes;
        [SerializeField] private long _biologicalDayHours;
        [SerializeField] private long _minNightHours;
        [SerializeField] private long _maxNightHours;
        [SerializeField] private long _scheduleChangeIntervalDays;

        public long VisualDayMinutes => _visualDayMinutes;

        public long BiologicalDayHours => _biologicalDayHours;

        public long MinNightHours => _minNightHours;

        public long MaxNightHours => _maxNightHours;

        public long ScheduleChangeIntervalDays => _scheduleChangeIntervalDays;
    }
}
