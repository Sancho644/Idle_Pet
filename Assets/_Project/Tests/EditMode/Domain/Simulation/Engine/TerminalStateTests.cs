using System;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    public sealed class TerminalStateTests
    {
        [TestCase(4L)]
        [TestCase(8L)]
        public void TerminalTransitionStopsLifeWhileTheWorldContinues(long target)
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -2 : 0);
            var engine = CreateEngine(cfg,
                ("prior", (c, _) => c.NextUInt32(RandomStreamId.Lifecycle)),
                ("end", (c, _) => Assert.That(c.TerminatePet(Pet), Is.True)),
                ("stale", (c, _) => c.NextUInt32(RandomStreamId.Birth)),
                ("repeat", (c, _) => Assert.That(c.TerminatePet(Pet), Is.False)),
                ("world", (c, _) => c.NextUInt32(RandomStreamId.Digestion)));
            var initial = engine.Initialize(At(0), Living(cfg), 42, new[]
            {
                Event(4, "prior", 1, pet: Pet), Event(4, "end", 2, pet: Pet),
                Event(4, "stale", 3, pet: Pet), Event(5, "stale", pet: Pet),
                Event(5, "repeat"), Event(7, "world")
            }).State;
            var result = engine.AdvanceTo(initial, At(target));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(result.Reached, Is.EqualTo(At(target)));
            Assert.That(result.State.ActivePet.Id, Is.EqualTo(Pet));
            Assert.That(result.State.ActivePet.TerminatedAt, Is.EqualTo(At(4)));
            Assert.That(result.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(92));
            Assert.That(result.State.EventQueue.NextSequence, Is.EqualTo(6));
            var types = target == 4 ? new[] { "prior", "end" } : new[] { "prior", "end", "repeat", "world" };
            Assert.That(result.Trace.Select(entry => entry.Request.TypeId), Is.EqualTo(types));
            var rng = RandomStreams.Create(42);
            rng.NextUInt32(RandomStreamId.Lifecycle);
            if (target > 4)
            {
                rng.NextUInt32(RandomStreamId.Digestion);
            }

            Assert.That(result.State.Rng.Streams, Is.EqualTo(rng.CaptureSnapshot().Streams));
            var recreated = Restore(result.State);
            var advanced = engine.AdvanceTo(recreated, At(15));
            Assert.That(advanced.State.ActivePet.Id, Is.EqualTo(Pet));
            Assert.That(advanced.State.ActivePet.TerminatedAt, Is.EqualTo(At(4)));
            Assert.That(advanced.State.ActivePet.Needs.Values, Is.EqualTo(result.State.ActivePet.Needs.Values));
            Assert.That(advanced.State.EventQueue.Events, Is.Empty);
            if (target == 8)
            {
                Evidence("terminal-transition", initial, result, advanced);
            }
        }

        [Test]
        public void LaterFailureOnTheSameTimestampRollsBackTerminalCancellation()
        {
            var cfg = Config(rates: _ => 1);
            var engine = CreateEngine(cfg, ("end", (c, _) => c.TerminatePet(Pet)), ("living", (_, _) => { }),
                ("fault", (_, _) => throw new SimulationRuleException("world failure")));
            var initial = engine.Initialize(At(0), Living(cfg), 42,
                new[] { Event(3, "end", 1, pet: Pet), Event(3, "fault", 2), Event(3, "living", 3, pet: Pet),
                    Event(4, "living", pet: Pet) }).State;

            var result = engine.AdvanceTo(initial, At(6));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(result.Diagnostic.Event.Request.TypeId, Is.EqualTo("fault"));
            Assert.That(result.Trace, Is.Empty);
            Assert.That(result.State.ActivePet.TerminatedAt, Is.Null);
            SameState(initial, result.State);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PetLifeEventsRequireTheMatchingLivingActivePet(bool hasPet)
        {
            var cfg = Config();
            var engine = CreateEngine(cfg, ("mark", (_, _) => Assert.Fail("Unexpected pet life execution.")));
            var pet = hasPet ? Living(cfg) : null;
            var result = engine.Initialize(At(0), pet, 42, new[] { Event(1, pet: new PetId("other.pet")) });

            AssertInitializationFailed(result);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.InvalidPet));
        }

        [Test]
        public void ZeroHealthAloneDoesNotCauseTerminalTransition()
        {
            var cfg = Config(rates: id => id == NeedId.Health ? -100 : 0);
            var engine = CreateEngine(cfg);
            var result = engine.AdvanceTo(new GameState(0, Living(cfg), 42), At(1));

            Assert.That(result.State.ActivePet.Needs[NeedId.Health].ExactNumerator, Is.Zero);
            Assert.That(result.State.ActivePet.TerminatedAt, Is.Null);
        }
    }
}
