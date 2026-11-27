using System;
using System.Linq;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    public sealed class SimulationInitializationTests
    {
        [Test]
        public void InitialEventFailureDoesNotProvideAnAdvanceableSnapshot()
        {
            var engine = CreateEngine(Config(),
                ("start", (_, _) => throw new SimulationRuleException("initial event failed")));

            var result = engine.Initialize(At(7), null, 42, new[] { Event(7, "start") });

            AssertInitializationFailed(result);
            Assert.That(result.Origin, Is.EqualTo(At(7)));
            Assert.That(result.Target, Is.EqualTo(At(7)));
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.RuleFailure));
            Assert.That(result.Diagnostic.Timestamp, Is.EqualTo(At(7)));
            Assert.That(result.Diagnostic.Event.Request.TypeId, Is.EqualTo("start"));
            Assert.That(result.Diagnostic.Event.Sequence, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => engine.AdvanceTo(result.State, At(8)));
        }

        [Test]
        public void ChildFailureDiscardsTheAttemptAndAFullRetryMatchesFreshInitialization()
        {
            const ulong seed = 42;
            var configuration = Config();
            var pet = Living(configuration);
            var start = At(7);
            var requests = new[] { Event(7, "start", pet: Pet), Event(12, "future") };
            bool fail = true;

            SimulationEngine BuildEngine(Func<bool> shouldFail) => CreateEngine(configuration,
                ("start", (context, _) =>
                {
                    context.SetNeed(Pet, NeedId.Satiety, Exact(150));
                    context.NextUInt32(RandomStreamId.Birth);
                    context.NextBounded(RandomStreamId.Evolution, 9);
                    context.Schedule(Event(7, "child", pet: Pet));
                    context.Schedule(Event(10, "future"));
                }),
                ("child", (context, _) =>
                {
                    context.NextUInt32(RandomStreamId.Digestion);
                    context.Schedule(Event(11, "future"));
                    if (shouldFail())
                    {
                        throw new SimulationRuleException("initial child failed");
                    }
                }),
                ("future", (_, _) => { }));

            var engine = BuildEngine(() => fail);
            var failure = engine.Initialize(start, pet, seed, requests);
            var repeated = engine.Initialize(start, pet, seed, requests);

            AssertInitializationFailed(failure);
            AssertInitializationFailed(repeated);
            Assert.That(failure.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.RuleFailure));
            Assert.That(failure.Diagnostic.Timestamp, Is.EqualTo(start));
            Assert.That(failure.Diagnostic.Event.Request.TypeId, Is.EqualTo("child"));
            Assert.That(failure.Diagnostic.Event.Sequence, Is.EqualTo(2L));
            Assert.That(repeated.Diagnostic.Code, Is.EqualTo(failure.Diagnostic.Code));
            Assert.That(repeated.Diagnostic.Timestamp, Is.EqualTo(failure.Diagnostic.Timestamp));
            Assert.That(repeated.Diagnostic.Reason, Is.EqualTo(failure.Diagnostic.Reason));
            SameTrace(new[] { failure.Diagnostic.Event }, new[] { repeated.Diagnostic.Event });
            Assert.That(pet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(100));
            InitializationEvidence("initialization-failure", seed, pet, requests, failure, repeated);

            fail = false;
            var repaired = engine.Initialize(start, pet, seed, requests);
            var freshEngine = BuildEngine(() => false);
            var fresh = freshEngine.Initialize(start, pet, seed, requests);

            Assert.That(repaired.Status, Is.EqualTo(SimulationStatus.Initialized));
            Assert.That(repaired.Reached, Is.EqualTo(start));
            Assert.That(repaired.Diagnostic, Is.Null);
            Assert.That(repaired.CommittedTransactions, Is.EqualTo(1));
            Assert.That(repaired.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(150));
            Assert.That(repaired.State.EventQueue.NextSequence, Is.EqualTo(5L));
            Assert.That(repaired.State.EventQueue.Events.Select(entry => entry.Sequence), Is.EqualTo(new[] { 3L, 4L, 1L }));
            Assert.That(repaired.Trace.Select(entry => entry.Sequence), Is.EqualTo(new[] { 0L, 2L }));
            var expectedRng = RandomStreams.Create(seed);
            expectedRng.NextUInt32(RandomStreamId.Birth);
            expectedRng.NextBounded(RandomStreamId.Evolution, 9);
            expectedRng.NextUInt32(RandomStreamId.Digestion);
            Assert.That(repaired.State.Rng.Streams, Is.EqualTo(expectedRng.CaptureSnapshot().Streams));
            SameState(fresh.State, repaired.State);
            SameTrace(fresh.Trace, repaired.Trace);
            InitializationEvidence("initialization-retry-success", seed, pet, requests, repaired, fresh);

            var restored = Restore(repaired.State);
            var sameTime = engine.AdvanceTo(restored, start);
            var continued = engine.AdvanceTo(restored, At(12));
            var control = freshEngine.AdvanceTo(fresh.State, At(12));
            Assert.That(sameTime.Status, Is.EqualTo(SimulationStatus.NoChange));
            Assert.That(sameTime.Trace, Is.Empty);
            SameState(repaired.State, sameTime.State);
            Assert.That(continued.Trace.Select(entry => entry.Sequence), Is.EqualTo(new[] { 3L, 4L, 1L }));
            SameTrace(control.Trace, continued.Trace);
            SameState(control.State, continued.State);
            Assert.That(continued.State.Rng.Streams, Is.EqualTo(repaired.State.Rng.Streams));
        }

        [TestCase(2, true)]
        [TestCase(3, false)]
        public void FiniteInitialChainCanUseTheFullBudgetButCannotExceedIt(int count, bool succeeds)
        {
            var requests = Enumerable.Range(0, count).Select(_ => Event(7)).ToArray();
            var engine = CreateEngine(Config(limit: 2), ("mark", (context, _) => context.NextUInt32(RandomStreamId.Birth)));

            var result = engine.Initialize(At(7), null, 42, requests);

            if (succeeds)
            {
                Assert.That(result.Status, Is.EqualTo(SimulationStatus.Initialized));
                Assert.That(result.State.ProcessedEventsAtCursor, Is.EqualTo(count));
                Assert.That(result.Trace.Count, Is.EqualTo(count));
            }
            else
            {
                AssertInitializationFailed(result);
                Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
                Assert.That(result.Diagnostic.Limit, Is.EqualTo(2));
                Assert.That(result.Diagnostic.Event.Sequence, Is.EqualTo(2L));
            }
        }

        [Test]
        public void InvalidInitialPetReturnsDiagnosticBeforeRunningEvents()
        {
            var configuration = Config();
            var pet = new PetState(Pet, Living(configuration).Needs, At(8));
            bool invoked = false;
            var engine = CreateEngine(configuration, ("mark", (_, _) => invoked = true));

            var result = engine.Initialize(At(7), pet, 42, new[] { Event(7) });

            AssertInitializationFailed(result);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.InvalidState));
            Assert.That(result.Diagnostic.Timestamp, Is.EqualTo(At(7)));
            Assert.That(result.Diagnostic.Event, Is.Null);
            Assert.That(invoked, Is.False);
        }
    }
}
