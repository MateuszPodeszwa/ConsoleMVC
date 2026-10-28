using System.Globalization;
using System.Net;
using ConsoleMVC.Preview;

return PreviewCommand.Run(args, Console.In, Console.Out, Console.Error);

namespace ConsoleMVC.Preview
{
    /// <summary>
    /// The <c>cvw-preview</c> command line: <c>cvw-preview &lt;file.cvw&gt; [--width 80] [--format text|ansi|html]
    /// [--stdin] [--high-contrast]</c>. With <c>--stdin</c> the view's text is read from standard input (an editor's
    /// unsaved buffer) and the file only says where it lives, for its partial views and layout.
    /// </summary>
    /// <example><code>cvw-preview Views/Home/IndexView.cvw --width 100 --format html</code></example>
    internal static class PreviewCommand
    {
        /// <summary>The exit code for a preview that was drawn (with or without diagnostics).</summary>
        public const int Success = 0;

        /// <summary>The exit code for wrong arguments.</summary>
        public const int UsageError = 1;

        /// <summary>The exit code for a view file that cannot be read.</summary>
        public const int FileError = 2;

        private const string Usage =
            "Usage: cvw-preview <file.cvw> [--width 80] [--format text|ansi|html] [--stdin] [--high-contrast]\n" +
            "Draws a ConsoleMVC view without building the application. --stdin reads the view's text from standard input.";

        /// <summary>Runs the command.</summary>
        /// <param name="args">The arguments.</param>
        /// <param name="input">Standard input, read with <c>--stdin</c>.</param>
        /// <param name="output">Where the preview is written.</param>
        /// <param name="error">Where usage errors and diagnostics are written (except in HTML, which lists them).</param>
        /// <returns>The exit code.</returns>
        public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
        {
            string? file = null;
            var options = new PreviewOptions();
            var fromInput = false;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--width" when i + 1 < args.Length
                                        && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var width):
                        options = options with { Width = width };
                        i++;
                        break;
                    case "--format" when i + 1 < args.Length && Enum.TryParse<PreviewFormat>(args[i + 1], ignoreCase: true, out var format)
                                                             && Enum.IsDefined(format):
                        options = options with { Format = format };
                        i++;
                        break;
                    case "--stdin":
                        fromInput = true;
                        break;
                    case "--high-contrast":
                        options = options with { HighContrast = true };
                        break;
                    case "--help" or "-h":
                        output.WriteLine(Usage);
                        return Success;
                    default:
                        if (args[i].StartsWith('-') || file is not null)
                        {
                            error.WriteLine($"cvw-preview: unexpected argument '{args[i]}'.\n{Usage}");
                            return UsageError;
                        }

                        file = args[i];
                        break;
                }
            }

            if (file is null && !fromInput)
            {
                error.WriteLine(Usage);
                return UsageError;
            }

            string text;
            try
            {
                text = fromInput ? input.ReadToEnd() : File.ReadAllText(file!);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                error.WriteLine($"cvw-preview: cannot read '{file}': {e.Message}");
                return FileError;
            }

            var files = file is null ? ProjectViewFiles.None : ProjectViewFiles.ForView(file);
            var result = CvwPreview.Render(text, files, options);
            output.Write(result.Output);

            if (options.Format == PreviewFormat.Html)
                WriteHtmlProblems(result, output);
            else
                WriteProblems(result, error);

            return Success;
        }

        private static void WriteProblems(PreviewResult result, TextWriter error)
        {
            foreach (var d in result.Diagnostics)
                error.WriteLine($"{d.Line}:{d.Column}: {(d.IsError ? "error" : "warning")} {d.Id}: {d.Message}");
            foreach (var note in result.Notes)
                error.WriteLine("note: " + note);
        }

        private static void WriteHtmlProblems(PreviewResult result, TextWriter output)
        {
            if (result.Diagnostics.Count == 0 && result.Notes.Count == 0)
                return;

            output.Write("<ul class=\"cvw-problems\">\n");
            foreach (var d in result.Diagnostics)
            {
                output.Write($"<li class=\"{(d.IsError ? "cvw-error" : "cvw-warning")}\" data-line=\"{d.Line}\" data-column=\"{d.Column}\">");
                output.Write(WebUtility.HtmlEncode($"Line {d.Line}, column {d.Column}: {d.Id} {d.Message}"));
                output.Write("</li>\n");
            }

            foreach (var note in result.Notes)
                output.Write($"<li class=\"cvw-note\">{WebUtility.HtmlEncode(note)}</li>\n");
            output.Write("</ul>\n");
        }
    }
}
