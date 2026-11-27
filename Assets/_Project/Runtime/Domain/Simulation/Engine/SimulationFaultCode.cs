namespace IdlePet.Domain.Simulation.Engine
{
    public enum SimulationFaultCode
    {
        InvalidState,
        UnknownEventType,
        PastEvent,
        InvalidPet,
        EventLimit,
        Overflow,
        RuleFailure,
        UnsupportedRuleSet
    }
}
