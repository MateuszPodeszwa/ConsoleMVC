using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// A view that only shows a menu, for testing how menu options become navigation. Its name does not end with
/// <c>View</c>, so the router does not try to map it.
/// </summary>
public sealed class MenuHost : ConsoleView<object?>
{
    private readonly MenuElement _menu;

    public MenuHost(MenuElement menu, ITextInput input)
    {
        _menu = menu;
        Input = input;
        Renderer = new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => 40);
    }

    public override NavigationResult Render(object? Model) => RunMenu(_menu);
}
