namespace IdlePet.Domain.Simulation.Engine
{
    public enum SimulationStatus
    {
        Advanced,
        NoChange,
        BackwardTarget,
        Initialized,
        CommandApplied,
        CommandRejected,
        Fault,
        InitializationFailed,
        UnsupportedRuleSet
    }
}
