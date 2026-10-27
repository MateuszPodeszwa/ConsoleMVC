using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

// Controllers and views driven by the ConsoleMVC.Testing tests (docs/design/testing-package.md).

namespace ConsoleMVC.Tests.Mvc.KioskApp
{
    /// <summary>A greeting the tests replace with their own.</summary>
    public interface IKioskGreeter
    {
        string Greet(string name);
    }

    public sealed class KioskGreeter : IKioskGreeter
    {
        public string Greet(string name) => $"Hello, {name}";
    }
}

namespace ConsoleMVC.Tests.Mvc.KioskApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.KioskApp;

    public sealed class KioskController(IKioskGreeter greeter) : Controller
    {
        public ActionResult Index() => View();

        public ActionResult Form() => View();

        public ActionResult Done(string name, string colour) => View((object)$"{greeter.Greet(name)} ({colour})");

        public ActionResult Loop() => View();
    }
}

namespace ConsoleMVC.Tests.Mvc.KioskApp.Views.Kiosk
{
    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            var text = new TextElement();
            text.AppendText("Welcome");
            Renderer.Render(text);
            Renderer.Render(new ShortcutsElement { Items = ShortcutHints }); // nothing when there are none

            var menu = new MenuElement();
            foreach (var (key, target, label) in new[] { ("1", "Form", "Form"), ("l", "Loop", "Loop") })
            {
                var option = new MenuOptionElement { Key = key, Target = target };
                option.AppendText(label);
                menu.Add(option);
            }

            var quit = new MenuOptionElement { Key = "q", Quit = true };
            quit.AppendText("Quit");
            menu.Add(quit);
            return RunMenu(menu);
        }
    }

    public sealed class FormView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            ReadInput(new InputElement { Name = "Name", Label = "Name: " });

            var colour = new SelectElement { Name = "Colour", Label = "Colour:" };
            foreach (var value in new[] { "red", "green", "blue" })
            {
                var choice = new ChoiceElement();
                choice.AppendText(value);
                colour.Add(choice);
            }

            ReadInput(colour);
            return NavigationResult.ToAction("Done", Form);
        }
    }

    public sealed class DoneView : ConsoleView<string>
    {
        public override NavigationResult Render(string Model)
        {
            var text = new TextElement();
            text.AppendText(Model);
            Renderer.Render(text);
            return NavigationResult.Quit();
        }
    }

    /// <summary>Navigates to itself without ever waiting for the user.</summary>
    public sealed class LoopView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => NavigationResult.ToAction("Loop");
    }
}
