using System;

namespace IdlePet.Domain.Simulation.Time
{
    public static class SimulationClock
    {
        public static LogicalTime GetTime(GameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new LogicalTime(state.LogicalTimeMilliseconds);
        }

        public static bool TryGetElapsed(LogicalTime current, LogicalTime target, out LogicalDuration elapsed)
        {
            if (target.CompareTo(current) < 0)
            {
                elapsed = default;
                return false;
            }

            elapsed = new LogicalDuration(checked(target.Milliseconds - current.Milliseconds));
            return true;
        }
    }
}
