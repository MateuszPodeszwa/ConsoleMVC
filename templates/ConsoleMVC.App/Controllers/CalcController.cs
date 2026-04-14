using ConsoleMVC.App.Models;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.App.Controllers;

/// <summary>
/// Demonstrates form data posting with simple parameter binding.
/// Unlike <see cref="GreetController"/> which binds to a model class, this controller
/// receives individual parameters (int, string) directly from the form data.
/// </summary>
public class CalcController : Controller
{
    /// <summary>
    /// Displays the calculator input form.
    /// </summary>
    public ActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Receives two numbers and an operator from the form and displays the result.
    /// Each parameter is individually bound from the form data by name.
    /// </summary>
    /// <param name="a">The first operand, bound from form data key "a".</param>
    /// <param name="b">The second operand, bound from form data key "b".</param>
    /// <param name="op">The operator (+, -, *, /), bound from form data key "op".</param>
    public ActionResult Result(int a, int b, string op)
    {
        var (expression, result) = op switch
        {
            "-" => ($"{a} - {b}", (a - b).ToString()),
            "*" => ($"{a} * {b}", (a * b).ToString()),
            "/" when b != 0 => ($"{a} / {b}", (a / b).ToString()),
            "/" => ($"{a} / {b}", "Error: division by zero"),
            _ => ($"{a} + {b}", (a + b).ToString()) // default to addition
        };

        return View(new CalcResultModel
        {
            Expression = expression,
            Result = result
        });
    }
}
