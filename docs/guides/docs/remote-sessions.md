# Remote Sessions

One ConsoleMVC process can serve the application to many people at once. Each person gets their own **session**: their
own screen, input, current page, Back history and session values, while controllers, views, middleware and services
are shared as in a local run.

## Serve without changing code

Set `ConsoleMVC:Serve` and run the application as usual:

```bash
dotnet run -- --ConsoleMVC:Serve=2323
```

Then connect from other terminals:

```bash
telnet localhost 2323
```

The server listens on the loopback address until you choose otherwise, so nobody else on the network can connect.
Press Ctrl+C in the server's console to stop it: every client is disconnected and their terminals are put back.

| Setting | Meaning | Default |
|---|---|---|
| `ConsoleMVC:Serve` | Where to listen: `2323`, `127.0.0.1:2323`, `0.0.0.0:2323` or `[::]:2323` | not set: run locally |
| `ConsoleMVC:ServeProtocol` | `Telnet`, or `Raw` for clients that are already in raw mode | `Telnet` |
| `ConsoleMVC:ServeCertificate` | A `.pfx` file that turns on TLS | none |
| `ConsoleMVC:ServeCertificatePassword` | Its password (keep it in user secrets or an environment variable) | none |
| `ConsoleMVC:ServeMaxSessions` | The most sessions at once | 16 |
| `ConsoleMVC:ServeAllowUnencrypted` | Allow a network address without TLS | `false` |

## Serve from code

```csharp
using var app = builder.Build();

await app.ServeAsync(new TerminalServerOptions
{
    EndPoint = new IPEndPoint(IPAddress.Loopback, 2323),
    MaxSessions = 50,
    IdleTimeout = TimeSpan.FromMinutes(10)
}, cancellationToken);
```

`ServeAsync` returns when the token is cancelled, after every session has ended. For more control, create the server
yourself:

```csharp
await using var server = new TerminalServer(app, new TerminalServerOptions());
server.Start();
Console.WriteLine($"Connect with: telnet {server.LocalEndPoint.Address} {server.LocalEndPoint.Port}");
```

## Knowing who is connected

Ask for `ITerminalSession` in a controller's constructor, with `@inject` in a view, or from a middleware's services. It
tells sessions apart and keeps values for one user for as long as they are connected:

```csharp
public sealed class AccountController(ITerminalSession session, IUserStore users) : Controller
{
    public ActionResult Index() =>
        session.Items.TryGetValue("user", out var user) ? View(user) : RedirectToAction("SignIn");

    public ActionResult SignIn(string name, string password)
    {
        if (!users.Check(name, password))
            return View();

        session.Items["user"] = name;
        return RedirectToAction("Index");
    }
}
```

| Member | Meaning |
|---|---|
| `Id` | `local` for the console, a new random identifier for every remote client |
| `IsRemote` | Whether this is a remote client |
| `RemoteEndPoint` | The client's address |
| `TerminalType` | What the client reported, such as `XTERM-256COLOR` |
| `Width`, `Height` | The client's window size, kept up to date when it resizes |
| `Items` | Values for this session only, safe to use from several threads |
| `Disconnect()` | Ends the session, for example after too many failed sign-ins |

Singletons are shared by every session, so keep per-user state in `Items` (or in a store keyed by the session's `Id`),
and make shared services safe to use from several threads. Scoped services are still one scope per screen. Ask for
`ITerminalSession` in controllers, views, middleware and scoped services, never in a singleton's constructor: a
singleton is created once, so it would keep the first session that created it.

## What works remotely

Everything a local run does: menus, forms and validation, arrow-key fields, `<scroll>`, keyboard shortcuts, the mouse,
full-screen mode, `@refresh` screens, live elements (`ILiveDisplay` draws on the session that started it), the
screen-reader mode and localisation. The screen is laid out for each client's width and laid out again when they resize
the window.

Two things stay on the server: `Console.WriteLine` in your own code writes to the server's console, and so does an
`IMarkupRenderer` or `ITextInput` resolved from services. Views draw with their own `Renderer` and `Input`, which the
framework sets for each session.

Ctrl+C ends a remote session (it does not stop the server), and so does a client that stays idle longer than
`IdleTimeout` (30 minutes unless you choose).

## Security

The defaults are safe:

- the server listens on the loopback address only;
- it refuses to listen on a network address without TLS, because everything typed, passwords included, would cross the
  network in clear text, unless you set `AllowUnencryptedNetworkAccess` (only on a network you trust);
- it accepts at most 16 sessions, buffers at most 4096 typed characters per session, keeps window sizes to at most
  1000 by 500, and drops idle clients and clients that do not finish the TLS handshake in 10 seconds;
- a client that stops reading cannot hold its session: a write that blocks for `WriteTimeout` (30 seconds) disconnects
  it, and stopping the server closes any connection whose session has not ended after 5 seconds;
- a filter (`AcceptConnection`) that throws refuses that connection and is logged; the server carries on;
- session identifiers are random, from a cryptographic generator.

Turn on TLS 1.2 or 1.3 with a certificate:

```csharp
options.Certificate = X509CertificateLoader.LoadPkcs12FromFile("server.pfx", password);
```

TLS clients connect with, for example, `socat -,raw,echo=0 OPENSSL:shop.example.com:2323` (use the `Raw` protocol for
them). The server does not sign users in: add a sign-in screen as above, filter addresses with
`TerminalServerOptions.AcceptConnection`, or put SSH in front.

## Over SSH

OpenSSH authenticates users and encrypts the connection, which makes it the best choice on the internet. There are two
ways to use it:

- **A process per user**, with nothing to set up in the application: make the application the user's command in
  `sshd_config`. A [Native AOT](native-aot.md) build starts in milliseconds.

  ```text
  Match User shop
      ForceCommand /opt/shop/Shop
  ```

- **One process for everyone**: serve on the loopback address with the `Raw` protocol, and forward SSH users to it.
  The SSH terminal must be put in raw mode first, so keys pass straight through:

  ```text
  Match User shop
      ForceCommand sh -c 'stty raw -echo; exec nc 127.0.0.1 2323'
  ```

  The application then sees every SSH user as a connection from the loopback address; sign them in with a screen of
  your own if it needs to know who they are.

## In a web browser

[ttyd](https://github.com/tsl0922/ttyd) shows a terminal program in a browser page:

```bash
ttyd --writable ./Shop                       # a process per browser tab
ttyd --writable telnet 127.0.0.1 2323        # one process, a session per tab
```

## Other transports

`RemoteTerminal` turns any pair of streams into a terminal, so a WebSocket, an SSH library's channel or a serial port
can run a session:

```csharp
await using var terminal = new RemoteTerminal(stream, new RemoteTerminalOptions { Columns = 100, Rows = 30 });
connection.Resized += (columns, rows) => terminal.Resize(columns, rows);
await app.RunSessionAsync(terminal, cancellationToken);
```

The other end must send keys as they are pressed and echo nothing (raw mode); the terminal decodes arrows, function
keys and modifiers, and writes ANSI text with CR LF line endings.

## Testing

`ConsoleMVC.Testing` runs the application locally, which is how sessions behave too. To test that two users are kept
apart, run two sessions on `RemoteTerminal`s over in-memory streams, or start a `TerminalServer` on port 0 and connect
with `TcpClient` (`server.LocalEndPoint` has the port chosen).
