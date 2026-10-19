using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.App.Tests;

public class ScaffoldSmokeTests
{
    [WpfFact]
    public void ThemeTokens_LoadFromResourceDictionary()
    {
        Assert.Equal(Colors.Black, ThemeResources.GetColor("script-paper"));
        Assert.True(ThemeResources.TryGetColor("Ilu.Color.Live", out _));
    }
}
