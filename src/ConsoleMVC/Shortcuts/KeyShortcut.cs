namespace ConsoleMVC.Mvc;

/// <summary>
/// An application-wide keyboard shortcut mapped with <see cref="MvcApplication.MapKey(KeyGesture, NavigationResult, string?)"/>:
/// pressing <see cref="Gesture"/> while a screen waits for input leaves that screen with the navigation from
/// <see cref="Navigate"/>.
/// </summary>
/// <param name="Gesture">The key.</param>
/// <param name="Navigate">Returns where to go; called each time the key is pressed.</param>
/// <param name="Description">What the shortcut does, for <c>&lt;shortcuts /&gt;</c>; shortcuts without one are not listed.</param>
/// <example><code>var help = new KeyShortcut(new KeyGesture(ConsoleKey.F1), () => NavigationResult.To("Help/Index"), "Help");</code></example>
public sealed record KeyShortcut(KeyGesture Gesture, Func<NavigationResult> Navigate, string? Description = null);
