using System.ComponentModel.DataAnnotations;

namespace Acceptance.Models;

/// <summary>The sign-up form of docs/guides/docs/validation.md, as written there.</summary>
public class SignupModel
{
    /// <summary>The user's name; required. Example: <c>Ada</c>.</summary>
    [Required, StringLength(30)]
    public string Name { get; set; } = "";

    /// <summary>The user's age, 13 to 120. Example: <c>36</c>.</summary>
    [Display(Name = "Your age"), Range(13, 120)]
    public int Age { get; set; } = 18;

    /// <summary>An optional e-mail address. Example: <c>ada@example.com</c>.</summary>
    [EmailAddress]
    public string? Email { get; set; }
}
