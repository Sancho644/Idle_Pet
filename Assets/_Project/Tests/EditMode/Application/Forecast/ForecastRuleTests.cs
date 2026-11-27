using System;
using System.Linq;
using System.Reflection;
using IdlePet.Application.Forecast;
using IdlePet.Domain;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Domain.Simulation.Rules;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Application.Forecast
{
    public sealed class ForecastRuleTests
    {
        private static int _staticCalls;

        [TestCase("closure")]
        [TestCase("instance")]
        [TestCase("static")]
        [TestCase("multicast")]
        public void DelegateRulesAreRejectedBeforeAnyHandlerEvenBeyondTheForecastHorizon(string kind)
        {
            int captured = 0;
            var instance = new Counter();
            Action<SimulationEventContext, ScheduledEvent> closure = (_, _) => captured++;
            Action<SimulationEventContext, ScheduledEvent> handler;
            switch (kind)
            {
                case "closure": handler = closure; break;
                case "instance": handler = instance.Handle; break;
                case "static": handler = StaticHandler; break;
                case "multicast": handler = closure + instance.Handle; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            _staticCalls = 0;
            var engine = CreateEngine(Config(), ("external", handler));
            var source = engine.Initialize(At(0), null, 42, new[] { Event(10, "external") }).State;
            var before = Restore(source);
            var service = new ForecastService(engine);

            var shortResult = service.Forecast(source, At(1));
            var longResult = service.Forecast(source, At(10));

            foreach (var result in new[] { shortResult, longResult })
            {
                Assert.That(result.Status, Is.EqualTo(SimulationStatus.UnsupportedRuleSet));
                Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.UnsupportedRuleSet));
                Assert.That(result.Diagnostic.Timestamp, Is.EqualTo(At(0)));
                Assert.That(result.State, Is.Null);
                Assert.That(result.Reached, Is.Null);
                Assert.That(result.Trace, Is.Empty);
                Assert.That(result.CommittedTransactions, Is.Zero);
            }
            Assert.That(captured, Is.Zero);
            Assert.That(instance.Calls, Is.Zero);
            Assert.That(_staticCalls, Is.Zero);
            SameState(before, source);
            if (kind == "closure")
            {
                Report("unsafe-handler-rejected", State("initial", before), State("source-after", source),
                    ForecastRecord("short", shortResult), ForecastRecord("forecast", longResult),
                    new System.Xml.Linq.XElement("calls", captured + instance.Calls + _staticCalls));
            }
        }

        [Test]
        public void AnEmptyDelegateRuleSetIsStillUnsupported()
        {
            var engine = CreateEngine(Config());
            var source = new GameState(0, null, 42);
            var before = Restore(source);
            var forecast = new ForecastService(engine).Forecast(source, At(0));

            Assert.That(forecast.Status, Is.EqualTo(SimulationStatus.UnsupportedRuleSet));
            Assert.That(forecast.State, Is.Null);
            SameState(before, source);
        }

        [Test]
        public void TrustedRegistryCopiesRuleDataAndRejectsInvalidOrDuplicateIds()
        {
            var rules = new[] { Rule("mark", Effect.Draw(RandomStreamId.Birth)) };
            var engine = Engine(Config(), rules);
            var source = engine.Initialize(At(0), null, 42, new[] { Event(3) }).State;
            var before = Restore(source);
            var expected = engine.AdvanceTo(source, At(3));

            rules[0].Item2[0] = Effect.Fail("mutated effect array");
            rules[0] = Rule("replaced");
            var result = new ForecastService(engine).Forecast(source, At(3));

            Matches(expected, result);
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(result.State.Rng.Streams, Is.Not.EqualTo(source.Rng.Streams));
            SameState(before, source);
            Assert.Throws<ArgumentException>(() => Engine(Config(), Rule("dup"), Rule("dup")));
            Assert.Throws<ArgumentException>(() => Engine(Config(), Rule("invalid id")));
            Assert.Throws<ArgumentException>(() => Engine(Config(), Rule(SimulationEngine.ThresholdEventTypeId)));
            Assert.Throws<ArgumentException>(() => Engine(Config(), Rule("missing.effects", new Effect[] { null })));
            Assert.Throws<ArgumentNullException>(() => new SimulationRuleSet(null));
            Assert.Throws<ArgumentException>(() => CreateEngine(Config(), ("dup", (_, _) => { }), ("dup", (_, _) => { })));
        }

        [Test]
        public void ForecastAndDomainUseOnlyTheIntendedAssemblyDependenciesAndHaveNoPublicDelegateRegistration()
        {
            var domain = typeof(GameState).Assembly;
            var application = typeof(ForecastService).Assembly;
            var projectRefs = application.GetReferencedAssemblies().Select(reference => reference.Name)
                .Where(name => name.StartsWith("IdlePet.", StringComparison.Ordinal));

            Assert.That(projectRefs, Is.EqualTo(new[] { domain.GetName().Name }));
            Assert.That(domain.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.No.Member(application.GetName().Name));
            foreach (var assembly in new[] { domain, application })
            {
                Assert.That(assembly.GetReferencedAssemblies().Select(reference => reference.Name)
                    .Any(name => name.StartsWith("Unity", StringComparison.Ordinal)), Is.False);
            }

            var parameters = typeof(SimulationEngine).GetConstructors().Single().GetParameters();
            Assert.That(parameters.Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(GameConfiguration) }));
            Assert.That(typeof(SimulationRuleSet).IsPublic, Is.False);
            Assert.That(typeof(ForecastService).GetFields(BindingFlags.Static | BindingFlags.DeclaredOnly |
                BindingFlags.Public | BindingFlags.NonPublic), Is.Empty);
        }

        private static void StaticHandler(SimulationEventContext _, ScheduledEvent __) => _staticCalls++;

        private sealed class Counter
        {
            internal int Calls { get; private set; }

            internal void Handle(SimulationEventContext _, ScheduledEvent __) => Calls++;
        }
    }
}
