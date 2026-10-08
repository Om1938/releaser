using System.Reflection;

namespace Releaser.Server;

internal static class BuildContext
{
    /// <summary>True when the build-time OpenAPI generator (GetDocument.Insider) hosts the app without a database.</summary>
    public static bool IsGeneratingOpenApi { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
