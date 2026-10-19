using IluPrompter.Core;

namespace IluPrompter.Input.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void KeyChord_UsesCanonicalModifierOrder() =>
        Assert.Equal("Ctrl+Alt+Shift+PageDown", ControlIds.KeyChord(true, true, true, "PageDown"));
}
