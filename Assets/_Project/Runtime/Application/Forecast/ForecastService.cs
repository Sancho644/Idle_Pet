using System;
using IdlePet.Domain;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Domain.State;

namespace IdlePet.Application.Forecast
{
    public sealed class ForecastService
    {
        private readonly SimulationEngine _engine;

        public ForecastService(SimulationEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public ForecastResult Forecast(GameState source, LogicalTime targetTime)
        {
            var origin = SimulationClock.GetTime(source);
            var blocker = _engine.GetForecastBlocker(origin);
            if (blocker != null)
            {
                return new ForecastResult(origin, targetTime, blocker);
            }

            return new ForecastResult(_engine.AdvanceTo(GameStateCloner.Clone(source), targetTime));
        }

        /// <summary>Forecasts from the source cursor by an explicit logical duration.</summary>
        /// <exception cref="OverflowException">The resulting logical time is not representable.</exception>
        public ForecastResult ForecastFor(GameState source, LogicalDuration duration) =>
            Forecast(source, SimulationClock.GetTime(source).Add(duration));
    }
}
