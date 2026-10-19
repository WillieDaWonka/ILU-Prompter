using System.Text.Json;
using IluPrompter.Core;

namespace IluPrompter.Core.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void DefaultSettings_RoundTripThroughJson()
    {
        var json = JsonSerializer.Serialize(PrompterSettings.Default, IluJson.Options);
        var back = JsonSerializer.Deserialize<PrompterSettings>(json, IluJson.Options);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, IluJson.Options));
        Assert.Equal(PrompterCommand.PlayPause, back!.Input.Keyboard.Transport["Space"].Display);
    }

    [Fact]
    public void LayoutGeometry_FindsLineAtY()
    {
        var g = new LayoutGeometry("k", 1, 100, 300, 300, [0, 100, 200], [100, 100, 100], [0, 0, 1], [0, 12, 0], [0, 200], Array.Empty<SectionMarker>(), 10);
        Assert.Equal(1, g.FindLineAt(150));
        Assert.Equal(0, g.FindLineAt(-50));
        Assert.Equal(2, g.FindLineForAnchor(new DocumentAnchor(1, 0)));
        Assert.Equal(1, g.FindLineForAnchor(new DocumentAnchor(0, 20)));
    }

    [Fact]
    public void Typography_MatchesDesignExample()
    {
        Assert.Equal(91.54, TypographyMath.BaseFontSize(1080 - 64 - 64, 8, 1.3), 2);
    }
}
