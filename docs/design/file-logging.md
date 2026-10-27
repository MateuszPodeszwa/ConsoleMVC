# Design: ConsoleMVC 1.22: File logging

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.22.0 |
| **Scope** | `builder.Logging.AddFile`, `FileLoggerProvider`, `FileLoggerOptions` |

---

## 1. Goal

A console application cannot show its logs: the console is the user interface. `AddFile` gives every ConsoleMVC
application somewhere to send them, with nothing to install.

```csharp
builder.Logging.AddFile("logs/app.log");
```

```
2026-10-27T21:04:05.123Z INFO  ConsoleMVC.Mvc.MvcApplication: Starting at Home/Index (Production)
2026-10-27T21:04:09.871Z WARN  Shop.Controllers.OrdersController: Order 42 has no address
2026-10-27T21:04:12.002Z ERROR ConsoleMVC.Mvc.MvcApplication: Unhandled exception on Orders/Save
    System.InvalidOperationException: The shop is closed.
       at Shop.Controllers.OrdersController.Save() in …
```

## 2. Classes (UML)

```
FileLoggerExtensions (namespace Microsoft.Extensions.Logging)
  + AddFile(this ILoggingBuilder, path?) / AddFile(this ILoggingBuilder, Action<FileLoggerOptions>)

[ProviderAlias("File")] FileLoggerProvider : ILoggerProvider          FileLoggerOptions
  + FileLoggerProvider(IOptions<FileLoggerOptions>, IHostEnvironment?)   + Path = "logs/app.log"
  + FileLoggerProvider(FileLoggerOptions, baseDirectory?, clock?)        + MaxFileSizeBytes = 10 MB
  + FilePath : string                                                   + RetainedFileCount = 5
  - queue : bounded Channel<string> ──▶ one background writer task       + QueueLength = 10 000
```

## 3. Rules (pseudocode)

```
AddFile: AddConfiguration(); one FileLoggerProvider (TryAddEnumerable); options bound from Logging:File, then the callback
path: relative to the content root (the application's folder)
Log: format "time LEVEL category: message" (+ exception), enqueue; never blocks
    continuation lines of the message and the exception are indented by four spaces; control characters are dropped,
    so a logged value cannot forge an entry or put escape sequences into the file
queue full: the entry is dropped and counted; the next written line reports how many were lost
writer: append with FileShare.ReadWrite (tail -f works); flush whenever the queue is empty
    bytes written ≥ MaxFileSizeBytes → close, app.(n-1).log → app.n.log … app.log → app.1.log, delete beyond RetainedFileCount
    IOException or no permission → stop writing; the application keeps running
Dispose: complete the queue, wait up to 5 s for the remaining entries
levels: the usual filters, including Logging:File:LogLevel
```
