using ConsoleMVC.Models;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.Views.Home;

public class IndexView : ConsoleView<HomeViewModel>
{
    public override NavigationResult Render(HomeViewModel model)
    {
        Console.WriteLine($"=== {model.Title} ===");
        Console.WriteLine();
        Console.WriteLine(model.Message);
        Console.WriteLine();

        for (var i = 0; i < model.MenuOptions.Count; i++)
            Console.WriteLine($"  [{i + 1}] {model.MenuOptions[i]}");

        Console.WriteLine();
        Console.Write("Select an option: ");
        var input = Console.ReadLine()?.Trim();

        return input switch
        {
            "1" => NavigationResult.To("Home", "About"),
            "2" => NavigationResult.Quit(),
            _ => NavigationResult.To("Home", "Index")
        };
    }
}
