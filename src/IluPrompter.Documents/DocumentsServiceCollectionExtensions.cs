using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Documents;

/// <summary>Stream E owns this file.</summary>
public static class DocumentsServiceCollectionExtensions
{
    public static IServiceCollection AddIluDocuments(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentNavigator, DocumentNavigator>();
        services.AddSingleton<ISectionParser, SectionParser>();
        services.AddSingleton<ISnapshotBuilder, SnapshotBuilder>();
        services.AddSingleton<IScriptFileService, ScriptFileService>();
        services.AddSingleton<IOutputDocumentFactory, OutputDocumentFactory>();
        services.AddSingleton<ILayoutGeometryBuilder, LayoutGeometryBuilder>();
        services.AddSingleton<IScriptFormatter, ScriptFormatter>();
        return services;
    }
}
