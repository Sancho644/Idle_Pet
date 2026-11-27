namespace IdlePet.Domain.Simulation.Random
{
    public readonly struct RandomStreamSnapshot
    {
        public RandomStreamSnapshot(RandomStreamId streamId, ulong sequenceId, ulong state)
        {
            StreamId = streamId;
            SequenceId = sequenceId;
            State = state;
        }

        public RandomStreamId StreamId { get; }

        public ulong SequenceId { get; }

        public ulong State { get; }
    }
}
