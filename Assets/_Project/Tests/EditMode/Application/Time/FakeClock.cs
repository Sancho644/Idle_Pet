using IdlePet.Application.Ports.Time;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Tests.EditMode.Application.Time
{
    internal sealed class FakeClock : ILogicalTimeSource
    {
        private LogicalTime _target;

        internal FakeClock(LogicalTime initialTime)
        {
            _target = initialTime;
        }

        public LogicalTime GetLogicalTime() => _target;

        internal void Advance(LogicalDuration elapsed)
        {
            _target = _target.Add(elapsed);
        }

        internal void Set(LogicalTime target)
        {
            _target = target;
        }
    }
}
