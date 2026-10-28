using System.ComponentModel.DataAnnotations;

namespace ConsoleMVC.App.Models;

/// <summary>
/// The table booking form, posted by <c>Views/Booking/IndexView.cvw</c>: a date, a number of guests, a table chosen
/// from a filtered list, extras chosen from several, and a PIN typed without being shown.
/// </summary>
/// <example><code>public ActionResult Confirm(BookingFormModel model) =&gt; ModelState.IsValid ? View("Confirmed", model) : View("Index");</code></example>
public sealed class BookingFormModel
{
    /// <summary>The name the table is booked under. Example: <c>Ada</c>.</summary>
    [Required, StringLength(30)]
    public string Name { get; set; } = "";

    /// <summary>The day, posted by <c>&lt;date&gt;</c> as <c>2026-10-27</c>. Example: tomorrow.</summary>
    public DateOnly Date { get; set; }

    /// <summary>How many people, checked by <c>[Range]</c> as well as by the <c>&lt;number&gt;</c> field. Example: <c>4</c>.</summary>
    [Range(1, 12)]
    public int Guests { get; set; } = 2;

    /// <summary>The table, chosen from a <c>&lt;select filter&gt;</c>. Example: <c>window-2</c>.</summary>
    [Required]
    public string Table { get; set; } = "";

    /// <summary>The extras, posted by <c>&lt;select multiple&gt;</c> as <c>cake,flowers</c>. Example: <c>["cake"]</c>.</summary>
    public List<string> Extras { get; set; } = [];

    /// <summary>A four-digit PIN to change the booking later, typed into a masked <c>&lt;input&gt;</c>. Example: <c>4821</c>.</summary>
    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "The PIN must be four digits.")]
    public string Pin { get; set; } = "";
}
