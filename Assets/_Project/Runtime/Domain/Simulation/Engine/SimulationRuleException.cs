using System;

namespace IdlePet.Domain.Simulation.Engine
{
    public sealed class SimulationRuleException : Exception
    {
        public SimulationRuleException(string message) : this(SimulationFaultCode.RuleFailure, message)
        {
        }

        internal SimulationRuleException(SimulationFaultCode code, string message) : base(message)
        {
            Code = code;
        }

        internal SimulationFaultCode Code { get; }
    }
}
