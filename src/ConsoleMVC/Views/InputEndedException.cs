namespace ConsoleMVC.Mvc;

/// <summary>
/// Thrown by <see cref="ConsoleViewBase.ReadInput"/> when the input ends (a pipe or file runs out, or the user presses
/// Ctrl+D or Ctrl+Z) while a form field is waiting for an answer. The application loop catches it and ends the run, as a
/// <c>&lt;menu&gt;</c> quits at the end of input, so a form is never posted with answers nobody gave (and a form shown
/// again after a failed validation cannot repeat for ever).
/// </summary>
/// <example><code>printf '2\n' | dotnet run   # the greeting form's Name field gets no answer: the application ends</code></example>
internal sealed class InputEndedException() : Exception("The input ended while a form field was waiting for an answer.");
