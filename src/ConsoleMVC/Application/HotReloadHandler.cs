using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(ConsoleMVC.Mvc.HotReloadHandler))]

namespace ConsoleMVC.Mvc;

/// <summary>
/// Receives .NET hot reload notifications (sent when <c>dotnet watch</c> applies an edit to a running application)
/// and tells running applications to rediscover their controllers and views before the next screen.
/// </summary>
/// <remarks>
/// The runtime finds this class through <see cref="MetadataUpdateHandlerAttribute"/> and calls
/// <see cref="ClearCache"/> and <see cref="UpdateApplication"/> by convention. Edited views and actions take effect
/// from the next screen; a view that is waiting for input keeps its old version until the user answers.
/// </remarks>
/// <example>
/// <code>
/// // In a terminal, from the project folder:
/// // dotnet watch
/// </code>
/// </example>
internal static class HotReloadHandler
{
    /// <summary>Raised after an update has been applied.</summary>
    public static event Action? Updated;

    /// <summary>
    /// Called by the runtime first, so caches can be cleared. The router's caches are rebuilt on the next screen.
    /// </summary>
    /// <param name="updatedTypes">The changed types, or <see langword="null"/> when unknown.</param>
    public static void ClearCache(Type[]? updatedTypes) => ServiceInjector.ClearCache(updatedTypes);

    /// <summary>
    /// Called by the runtime after an update has been applied.
    /// </summary>
    /// <param name="updatedTypes">The changed types, or <see langword="null"/> when unknown.</param>
    public static void UpdateApplication(Type[]? updatedTypes) => Updated?.Invoke();
}
