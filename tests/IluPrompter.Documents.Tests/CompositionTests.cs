using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Documents.Tests;

public class CompositionTests
{
    [Fact]
    public void AddIluDocuments_RegistersEveryContractAsASingleton()
    {
        var services = new ServiceCollection().AddIluDocuments();

        var registrations = services.ToDictionary(d => d.ServiceType, d => (d.ImplementationType, d.Lifetime));

        Assert.Equal(7, registrations.Count);
        Assert.All(registrations.Values, r => Assert.Equal(ServiceLifetime.Singleton, r.Lifetime));
        Assert.Equal(typeof(DocumentNavigator), registrations[typeof(IDocumentNavigator)].ImplementationType);
        Assert.Equal(typeof(SectionParser), registrations[typeof(ISectionParser)].ImplementationType);
        Assert.Equal(typeof(SnapshotBuilder), registrations[typeof(ISnapshotBuilder)].ImplementationType);
        Assert.Equal(typeof(ScriptFileService), registrations[typeof(IScriptFileService)].ImplementationType);
        Assert.Equal(typeof(OutputDocumentFactory), registrations[typeof(IOutputDocumentFactory)].ImplementationType);
        Assert.Equal(typeof(LayoutGeometryBuilder), registrations[typeof(ILayoutGeometryBuilder)].ImplementationType);
        Assert.Equal(typeof(ScriptFormatter), registrations[typeof(IScriptFormatter)].ImplementationType);
    }

    [Fact]
    public void ConstructorSignatures_StayAsScaffolded()
    {
        // Frozen (ARCHITECTURE section 7.1): the container builds these, so the parameters must not change.
        Assert.Equal([typeof(IDocumentNavigator), typeof(ISectionParser)], ParameterTypes(typeof(SnapshotBuilder)));
        Assert.Equal([typeof(ISnapshotBuilder)], ParameterTypes(typeof(OutputDocumentFactory)));
        Assert.Equal([typeof(IDocumentNavigator)], ParameterTypes(typeof(LayoutGeometryBuilder)));
        Assert.Empty(ParameterTypes(typeof(DocumentNavigator)));
        Assert.Empty(ParameterTypes(typeof(SectionParser)));
        Assert.Empty(ParameterTypes(typeof(ScriptFileService)));
        Assert.Empty(ParameterTypes(typeof(ScriptFormatter)));
    }

    private static Type[] ParameterTypes(Type type) =>
        type.GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToArray();
}
