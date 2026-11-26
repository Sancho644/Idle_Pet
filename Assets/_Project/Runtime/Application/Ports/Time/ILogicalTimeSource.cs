using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Application.Ports.Time
{
    public interface ILogicalTimeSource
    {
        // Returns a target for simulation. A source reading never updates processed state.
        LogicalTime GetLogicalTime();
    }
}
