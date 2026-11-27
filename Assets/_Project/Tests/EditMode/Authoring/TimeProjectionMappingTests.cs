using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class TimeProjectionMappingTests
    {
        [Test]
        public void RemappingAuthoredVisualPeriodChangesOnlyNewWorldProjection()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Time, asset => asset.FindProperty("_visualDayMinutes").longValue = 60);
            var initial = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release);
            Assert.That(initial.Succeeded, Is.True, string.Join("\n", initial.Issues));
            var point = new LogicalTime(5_400_000);
            var initialWorld = new WorldClock(initial.Definition.Time, default);
            var initialBiological = new CircadianClock(initial.Definition.Time, default);
            CyclePhase originalWorld = initialWorld.Project(point);
            CyclePhase originalBiological = initialBiological.Project(point);

            ConfigurationFixture.Edit(fixture.Time, asset => asset.FindProperty("_visualDayMinutes").longValue = 30);
            var remapped = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release);
            Assert.That(remapped.Succeeded, Is.True, string.Join("\n", remapped.Issues));
            var updatedWorld = new WorldClock(remapped.Definition.Time, default).Project(point);
            var updatedBiological = new CircadianClock(remapped.Definition.Time, default).Project(point);

            Assert.That(initial.Definition.Time.VisualDayMilliseconds, Is.EqualTo(3_600_000));
            Assert.That(remapped.Definition.Time.VisualDayMilliseconds, Is.EqualTo(1_800_000));
            Assert.That(originalWorld.CycleIndex, Is.EqualTo(1));
            Assert.That(originalWorld.ElapsedInCycleMilliseconds, Is.EqualTo(1_800_000));
            Assert.That(updatedWorld.CycleIndex, Is.EqualTo(3));
            Assert.That(updatedWorld.ElapsedInCycleMilliseconds, Is.Zero);
            Assert.That(initialWorld.Project(point), Is.EqualTo(originalWorld));
            Assert.That(initialBiological.Project(point), Is.EqualTo(originalBiological));
            Assert.That(updatedBiological, Is.EqualTo(originalBiological));
        }
    }
}
