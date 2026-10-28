using ConsoleMVC.App.Models;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.Logging;

namespace ConsoleMVC.App.Controllers;

/// <summary>
/// Books a table: the richer form fields (<c>&lt;date&gt;</c>, <c>&lt;number&gt;</c>, <c>&lt;select filter&gt;</c>,
/// <c>&lt;select multiple&gt;</c> and a masked <c>&lt;input&gt;</c>), <c>[Range]</c> validation and logging with
/// <see cref="ILogger{TCategoryName}"/> (to logs/app.log, see Program.cs).
/// </summary>
/// <param name="logger">Writes to the application's log; a booking is logged, its PIN never.</param>
public class BookingController(ILogger<BookingController> logger) : Controller
{
    /// <summary>Shows the booking form. Example: <c>nav="Booking/Index"</c>.</summary>
    public ActionResult Index() => View();

    /// <summary>
    /// Confirms a valid booking; shows the form again, with its errors, when anything is wrong. The answers that were
    /// valid stay filled in.
    /// </summary>
    /// <param name="model">The posted form, validated before this action runs.</param>
    public ActionResult Confirm(BookingFormModel model)
    {
        if (!ModelState.IsValid)
            return View("Index");

        // Structured logging: the values are kept as fields of the entry, and the file logger strips control characters
        logger.LogInformation("Booked {Table} for {Guests} on {Date} under {Name}", model.Table, model.Guests, model.Date, model.Name);
        return View("Confirmed", model);
    }
}
