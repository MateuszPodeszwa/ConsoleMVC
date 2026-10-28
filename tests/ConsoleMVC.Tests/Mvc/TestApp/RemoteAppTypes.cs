using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

// A controller and view served to remote terminals (docs/design/remote-sessions.md).

namespace ConsoleMVC.Tests.Mvc.RemoteApp.Controllers
{
    /// <summary>Counts the visits of each session in its items, so tests can tell sessions apart.</summary>
    public sealed class SessionController(ITerminalSession session) : Controller
    {
        public ActionResult Index()
        {
            var visits = session.Items.TryGetValue("visits", out var count) ? (int)count! + 1 : 1;
            session.Items["visits"] = visits;
            return View((object)$"Session {session.Id} visit {visits} remote={session.IsRemote} width={session.Width} type={session.TerminalType ?? "none"}");
        }

        public ActionResult Name() => View();

        public ActionResult Hello(string name) => View((object)$"Hello, {name}!");
    }
}

namespace ConsoleMVC.Tests.Mvc.RemoteApp.Views.Session
{
    public sealed class IndexView : ConsoleView<string>
    {
        public override NavigationResult Render(string Model)
        {
            var text = new TextElement();
            text.AppendText(Model);
            Renderer.Render(text);

            var menu = new MenuElement();
            foreach (var (key, target, label) in new[] { ("a", "Index", "Again"), ("n", "Name", "Name") })
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

    public sealed class NameView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            ReadInput(new InputElement { Name = "Name", Label = "Name: " });
            return NavigationResult.ToAction("Hello", Form);
        }
    }

    public sealed class HelloView : ConsoleView<string>
    {
        public override NavigationResult Render(string Model)
        {
            var text = new TextElement();
            text.AppendText(Model);
            Renderer.Render(text);
            return NavigationResult.ToAction("Index");
        }
    }
}
