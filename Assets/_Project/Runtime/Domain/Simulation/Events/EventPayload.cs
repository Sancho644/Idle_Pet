using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Simulation.Events
{
    public readonly struct EventPayload
    {
        public EventPayload(string text)
        {
            Text = text;
            NeedId = null;
            Threshold = null;
            Direction = null;
        }

        internal EventPayload(NeedThresholdCrossing crossing)
        {
            Text = null;
            NeedId = crossing.NeedId;
            Threshold = crossing.Threshold;
            Direction = crossing.Direction;
        }

        public string Text { get; }

        public NeedId? NeedId { get; }

        public NeedValue? Threshold { get; }

        public NeedThresholdDirection? Direction { get; }
    }
}
