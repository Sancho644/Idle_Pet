using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Commands;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Engine
{
    public sealed class SimulationEngine
    {
        public const string ThresholdEventTypeId = "need.threshold";

        private readonly GameConfiguration _configuration;
        private readonly Dictionary<string, Action<SimulationEventContext, ScheduledEvent>> _rules;

        public SimulationEngine(GameConfiguration configuration,
            IEnumerable<KeyValuePair<string, Action<SimulationEventContext, ScheduledEvent>>> rules = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _rules = new Dictionary<string, Action<SimulationEventContext, ScheduledEvent>>(StringComparer.Ordinal);
            if (rules == null)
            {
                return;
            }

            foreach (var entry in rules)
            {
                if (!ConfigurationId.IsValid(entry.Key) || entry.Value == null)
                {
                    throw new ArgumentException("Each rule needs a stable event type and a handler.", nameof(rules));
                }

                _rules.Add(entry.Key, entry.Value);
            }
        }

        public SimulationResult Initialize(LogicalTime time, PetState pet, ulong worldSeed, IEnumerable<EventRequest> initialEvents)
        {
            if (initialEvents == null)
            {
                throw new ArgumentNullException(nameof(initialEvents));
            }

            GameState checkpoint;
            try
            {
                checkpoint = new GameState(time.Milliseconds, pet, worldSeed);
                ValidateState(checkpoint);
            }
            catch (Exception error) when (IsDiagnostic(error))
            {
                return InitializationFailure(time, Diagnose(error, time, null, SimulationFaultCode.InvalidState));
            }

            var trace = new List<ScheduledEvent>();
            var state = Transact(checkpoint, time, pet, context =>
            {
                foreach (var request in initialEvents)
                {
                    context.Schedule(request);
                }
            }, trace, out var fault);
            return fault == null
                ? new SimulationResult(SimulationStatus.Initialized, time, time, state, trace, 1, 0)
                : InitializationFailure(time, fault);
        }

        public SimulationResult AdvanceTo(GameState state, LogicalTime targetTime)
        {
            var origin = SimulationClock.GetTime(state);
            var trace = new List<ScheduledEvent>();
            try
            {
                ValidateState(state);
            }
            catch (Exception error) when (IsDiagnostic(error))
            {
                return Failure(origin, targetTime, state, trace, 0, 0, Diagnose(error, origin, null, SimulationFaultCode.InvalidState));
            }

            int relation = targetTime.CompareTo(origin);
            if (relation <= 0)
            {
                var status = relation == 0 ? SimulationStatus.NoChange : SimulationStatus.BackwardTarget;
                return new SimulationResult(status, origin, targetTime, state, trace, 0, 0);
            }

            int transactions = 0;
            int thresholdBoundaries = 0;
            while (state.LogicalTimeMilliseconds < targetTime.Milliseconds)
            {
                var current = SimulationClock.GetTime(state);
                LogicalTime boundary = current;
                PetState pet = state.ActivePet;
                var crossings = new List<NeedThresholdCrossing>();
                try
                {
                    boundary = NextScheduledBoundary(state, targetTime);
                    if (pet != null && !pet.TerminatedAt.HasValue)
                    {
                        var rates = NeedRateCalculator.Calculate(_configuration, state.ActiveModifierIds);
                        boundary = FindThresholdBoundary(pet.Needs, rates, current, boundary, crossings);
                        var duration = new LogicalDuration(checked(boundary.Milliseconds - current.Milliseconds));
                        pet = new PetState(pet.Id, NeedsIntegrator.Integrate(pet.Needs, _configuration.Needs, rates, duration));
                    }
                }
                catch (Exception error) when (IsDiagnostic(error))
                {
                    return Failure(origin, targetTime, state, trace, transactions, thresholdBoundaries,
                        Diagnose(error, boundary, null, SimulationFaultCode.InvalidState));
                }

                var next = Transact(state, boundary, pet, context =>
                {
                    foreach (var crossing in crossings)
                    {
                        context.Schedule(new EventRequest(context.Time, 0, "need." + crossing.NeedId,
                            ThresholdEventTypeId, EventScope.PetLife, pet.Id, new EventPayload(crossing)));
                    }
                }, trace, out var fault);
                if (fault != null)
                {
                    return Failure(origin, targetTime, state, trace, transactions, thresholdBoundaries, fault);
                }

                state = next;
                transactions++;
                if (crossings.Count != 0)
                {
                    thresholdBoundaries++;
                }
            }

            return new SimulationResult(SimulationStatus.Advanced, origin, targetTime, state, trace, transactions, thresholdBoundaries);
        }

        public SimulationResult ExecuteCommand(GameState state, LogicalTime time, ISimulationCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var natural = AdvanceTo(state, time);
            if (natural.Status == SimulationStatus.Fault)
            {
                return natural;
            }

            if (natural.Status == SimulationStatus.BackwardTarget)
            {
                return CommandRejection(natural, "command.past");
            }

            string rejection;
            try
            {
                rejection = command.Validate(natural.State);
            }
            catch (Exception error) when (IsRecoverableRuleError(error))
            {
                return Failure(natural.Origin, time, natural.State, natural.Trace, natural.CommittedTransactions,
                    natural.ThresholdBoundaries, Diagnose(error, time, null, SimulationFaultCode.RuleFailure));
            }

            if (rejection != null)
            {
                return CommandRejection(natural, rejection);
            }

            var trace = new List<ScheduledEvent>(natural.Trace);
            var result = Transact(natural.State, time, natural.State.ActivePet, command.Apply, trace, out var fault);
            return fault == null
                ? new SimulationResult(SimulationStatus.CommandApplied, natural.Origin, time, result, trace,
                    natural.CommittedTransactions + 1, natural.ThresholdBoundaries)
                : Failure(natural.Origin, time, natural.State, trace, natural.CommittedTransactions, natural.ThresholdBoundaries, fault);
        }

        private static SimulationResult CommandRejection(SimulationResult natural, string reason)
        {
            return new SimulationResult(SimulationStatus.CommandRejected, natural.Origin, natural.Target,
                natural.State, natural.Trace, natural.CommittedTransactions, natural.ThresholdBoundaries, rejectionReason: reason);
        }

        private static LogicalTime NextScheduledBoundary(GameState state, LogicalTime target)
        {
            var events = state.EventQueue.Events;
            return events.Count != 0 && events[0].Request.Timestamp.CompareTo(target) < 0 ? events[0].Request.Timestamp : target;
        }

        private LogicalTime FindThresholdBoundary(NeedsState needs, EffectiveNeedRates rates, LogicalTime current,
            LogicalTime boundary, List<NeedThresholdCrossing> crossings)
        {
            long nearest = checked(boundary.Milliseconds - current.Milliseconds);
            foreach (NeedDefinition definition in _configuration.Needs.Entries.Values)
            {
                var reachable = NeedThresholdCalculator.FindCrossings(needs[definition.Id], rates[definition.Id],
                    new LogicalDuration(nearest), definition);
                foreach (var crossing in reachable)
                {
                    long elapsed = crossing.Elapsed.Milliseconds;
                    if (elapsed > nearest)
                    {
                        break;
                    }

                    if (elapsed < nearest)
                    {
                        nearest = elapsed;
                        crossings.Clear();
                    }

                    crossings.Add(crossing);
                }
            }

            return current.Add(new LogicalDuration(nearest));
        }

        private GameState Transact(GameState checkpoint, LogicalTime time, PetState pet,
            Action<SimulationEventContext> start, List<ScheduledEvent> committedTrace, out SimulationDiagnostic fault)
        {
            var context = new SimulationEventContext(_configuration, ValidateRequest, checkpoint, time, pet);
            var trace = new List<ScheduledEvent>();
            try
            {
                start(context);
                Stabilize(context, trace);
                var state = context.Commit();
                committedTrace.AddRange(trace);
                fault = null;
                return state;
            }
            catch (Exception error) when (IsRecoverableRuleError(error))
            {
                fault = Diagnose(error, time, context.CurrentEvent, SimulationFaultCode.RuleFailure);
                return checkpoint;
            }
            finally
            {
                context.Close();
            }
        }

        private void Stabilize(SimulationEventContext context, List<ScheduledEvent> trace)
        {
            int limit = _configuration.Simulation.MaxEventsPerTimestamp;
            while (context.Queue.First is ScheduledEvent next && next.Request.Timestamp.CompareTo(context.Time) == 0)
            {
                context.CurrentEvent = next;
                if (context.ProcessedEvents >= limit)
                {
                    throw new SimulationRuleException(SimulationFaultCode.EventLimit, "The causal queue exceeded its event budget for the timestamp.");
                }

                context.Queue.Remove(next);
                context.ProcessedEvents++;
                if (_rules.TryGetValue(next.Request.TypeId, out var handle))
                {
                    handle(context, next);
                }

                trace.Add(next);
                context.CurrentEvent = null;
            }
        }

        private void ValidateState(GameState state)
        {
            if (state.ProcessedEventsAtCursor > _configuration.Simulation.MaxEventsPerTimestamp)
            {
                throw new ArgumentException("The committed event count exceeds the configured timestamp budget.", nameof(state));
            }

            foreach (var scheduled in state.EventQueue.Events)
            {
                ValidateRequest(scheduled.Request);
            }

            foreach (string id in state.ActiveModifierIds)
            {
                if (!_configuration.RateModifiers.ContainsKey(id))
                {
                    throw new ArgumentException($"Unknown active modifier '{id}'.", nameof(state));
                }
            }

            if (state.ActivePet != null)
            {
                foreach (NeedDefinition entry in _configuration.Needs.Entries.Values)
                {
                    entry.ValidateValue(state.ActivePet.Needs[entry.Id], nameof(state));
                }
            }
        }

        private void ValidateRequest(EventRequest request)
        {
            if (request.TypeId == ThresholdEventTypeId)
            {
                var payload = request.Payload;
                if (request.Scope != EventScope.PetLife || !payload.NeedId.HasValue ||
                    !payload.Threshold.HasValue || !payload.Direction.HasValue ||
                    !_configuration.Needs.Entries.TryGetValue(payload.NeedId.Value, out var need) ||
                    !need.Thresholds.Contains(payload.Threshold.Value))
                {
                    throw new ArgumentException("A threshold event must identify a configured threshold of the living pet.", nameof(request));
                }
            }
            else if (!_rules.ContainsKey(request.TypeId))
            {
                throw new SimulationRuleException(SimulationFaultCode.UnknownEventType, $"Unknown event type '{request.TypeId}'.");
            }
        }

        private static bool IsDiagnostic(Exception error) =>
            error is SimulationRuleException || error is ArgumentException || error is InvalidOperationException ||
            error is OverflowException || error is KeyNotFoundException;

        private static bool IsRecoverableRuleError(Exception error) =>
            !(error is OutOfMemoryException || error is StackOverflowException || error is AccessViolationException ||
                error is AppDomainUnloadedException || error is BadImageFormatException || error is OperationCanceledException);

        private SimulationDiagnostic Diagnose(Exception error, LogicalTime time, ScheduledEvent entry, SimulationFaultCode fallback)
        {
            var code = error is SimulationRuleException rule ? rule.Code
                : error is OverflowException ? SimulationFaultCode.Overflow : fallback;
            int? limit = code == SimulationFaultCode.EventLimit ? _configuration.Simulation.MaxEventsPerTimestamp : (int?)null;
            return new SimulationDiagnostic(code, time, entry, error.Message, limit);
        }

        private static SimulationResult InitializationFailure(LogicalTime time, SimulationDiagnostic fault) =>
            new SimulationResult(SimulationStatus.InitializationFailed, time, time, null, Array.Empty<ScheduledEvent>(), 0, 0, fault);

        private static SimulationResult Failure(LogicalTime origin, LogicalTime target, GameState checkpoint,
            IEnumerable<ScheduledEvent> trace, int transactions, int thresholdBoundaries, SimulationDiagnostic fault) =>
            new SimulationResult(SimulationStatus.Fault, origin, target, checkpoint, trace, transactions, thresholdBoundaries, fault);
    }
}
