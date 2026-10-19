using IluPrompter.Rendering;

namespace IluPrompter.Rendering.Tests;

public class ScaffoldSmokeTests
{
    [WpfFact]
    public void Preview_CanBeConstructed() => Assert.NotNull(new PrompterPreview());
}
