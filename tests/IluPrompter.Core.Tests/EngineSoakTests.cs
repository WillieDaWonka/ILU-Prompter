namespace IluPrompter.Core.Tests;

public class EngineSoakTests
{
    private static readonly double[] InterestingValues =
    [
        0, 0.5, -0.5, 1, -1, 4.5, -4.5, 10, -10, 100, -100, 1e9, -1e9, 0.001, 119.6, -119.6,
        double.NaN, double.PositiveInfinity, double.NegativeInfinity,
    ];

    [Theory]
    [InlineData(20261019)]
    [InlineData(1)]
    [InlineData(987654321)]
    public void TenMinutesAt60Hz_WithRandomCommands_NeverProducesNaN_OrLeavesTheBounds(int seed)
    {
        var rng = new Random(seed);
        var h = new EngineHarness(RandomGeometry(rng, "s0"));
        var kinds = Enum.GetValues<EngineCommandKind>();
        const int ticks = 60 * 60 * 10;

        for (var i = 0; i < ticks; i++)
        {
            if (rng.NextDouble() < 0.04)
            {
                h.Engine.Post(RandomCommand(rng, kinds, h));
            }

            if (i % 1800 == 900)
            {
                h.SetLayout(RandomGeometry(rng, "s" + i), rng.NextDouble() < 0.5 ? null : TestGeometry.Viewport(100 + (rng.NextDouble() * 500)));
            }

            if (i % 2500 == 1000)
            {
                h.Settings.Update(s => s with
                {
                    Scroll = s.Scroll with
                    {
                        PauseAtSections = rng.NextDouble() < 0.5,
                        Acceleration = (Acceleration)rng.Next(3),
                    },
                });
            }

            var roll = rng.NextDouble();
            var dt = roll < 0.01 ? 0.3 + rng.NextDouble() : (roll < 0.25 ? 1.0 / 120 : 1.0 / 60);
            h.Step(dt);

            var p = h.Pos;
            Assert.True(double.IsFinite(p), $"tick {i}: position {p}");
            Assert.True(p >= h.Pmin - 1e-6 && p <= h.Pmax + 1e-6, $"tick {i}: {p} outside [{h.Pmin}, {h.Pmax}]");

            var s = h.Status;
            Assert.InRange(s.SetSpeed, -10.0, 10.0);
            Assert.True(double.IsFinite(s.EffectiveSpeed) && Math.Abs(s.EffectiveSpeed) <= 10.05, $"tick {i}: effective {s.EffectiveSpeed}");
            Assert.True(double.IsFinite(s.LinesPerMinute) && double.IsFinite(s.WordsPerMinute) && double.IsFinite(s.StepIntervalSeconds));
            Assert.False(double.IsNaN(s.SecondsRemaining));
            Assert.True(s.SecondsRemaining >= 0);
            Assert.InRange(s.LineIndex, s.LineCount == 0 ? -1 : 0, Math.Max(-1, s.LineCount - 1));
            Assert.Equal(s.Transport == TransportState.AtEnd, s.IsAtEnd);
            Assert.True(Enum.IsDefined(s.Transport));
            if (s.SectionCount > 0)
            {
                Assert.InRange(s.SectionIndex, -1, s.SectionCount - 1);
            }
        }
    }

    [Fact]
    public void SoakWithoutLayoutChurn_ExercisesPlayPauseAndTheEnds()
    {
        var rng = new Random(7);
        var h = new EngineHarness(TestGeometry.Create(lines: 60, sectionParagraphs: [2, 5]));
        var reachedEnd = false;
        var reachedStart = false;
        var played = false;
        for (var i = 0; i < 60 * 60 * 10; i++)
        {
            if (i % 400 == 0)
            {
                h.Engine.Post(rng.Next(4) switch
                {
                    0 => EngineCommand.SetSpeed(rng.Next(-10, 11)),
                    1 => EngineCommand.JumpToStart(),
                    2 => EngineCommand.Play(),
                    _ => EngineCommand.SeekToOffset(rng.NextDouble() * h.Pmax),
                });
            }

            h.Step(1.0 / 60);
            reachedEnd |= h.Status.IsAtEnd;
            reachedStart |= h.Status.IsAtStart;
            played |= h.Status.Transport == TransportState.Playing;
            Assert.InRange(h.Pos, h.Pmin - 1e-6, h.Pmax + 1e-6);
        }

        Assert.True(reachedEnd && reachedStart && played);
    }

    private static EngineCommand RandomCommand(Random rng, EngineCommandKind[] kinds, EngineHarness h)
    {
        var kind = rng.NextDouble() < 0.25 ? EngineCommandKind.Play : kinds[rng.Next(kinds.Length)];
        var value = rng.NextDouble() < 0.6
            ? InterestingValues[rng.Next(InterestingValues.Length)]
            : (rng.NextDouble() - 0.3) * h.Pmax;
        var index = rng.NextDouble() < 0.7 ? rng.Next(-3, 12) : rng.Next(-1000, 1000);
        return new EngineCommand(kind, value, index);
    }

    private static LayoutGeometry RandomGeometry(Random rng, string key)
    {
        var lines = rng.Next(0, 400);
        var linesPerParagraph = rng.Next(1, 9);
        var lineHeight = 60 + (rng.NextDouble() * 160);
        var paragraphs = lines == 0 ? 0 : ((lines - 1) / linesPerParagraph) + 1;
        var sections = new List<int>();
        for (var p = 0; p < paragraphs; p++)
        {
            if (rng.NextDouble() < 0.05) { sections.Add(p); }
        }

        return TestGeometry.Create(lines, lineHeight, linesPerParagraph, sections.ToArray(), key);
    }
}
