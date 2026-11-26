using IdlePet.Domain.Simulation.Engine;

namespace IdlePet.Domain.Simulation.Commands
{
    public interface ISimulationCommand
    {
        // Return null to accept, or a stable rejection reason. Validation is a pure query.
        string Validate(GameState state);

        void Apply(SimulationEventContext context);
    }
}
