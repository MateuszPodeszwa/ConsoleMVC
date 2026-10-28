namespace ConsoleMVC.Mvc;

/// <summary>
/// A feature module: a class library with its own controllers, views (and partials) and services, added to an application
/// with <see cref="MvcApplicationBuilder.AddModule{TModule}"/>. Its controllers and views are discovered with the
/// application's, and <see cref="Configure"/> registers what the module needs. Modules usually put their controllers in an
/// area, so their routes cannot collide with the application's.
/// </summary>
/// <example>
/// <code>
/// public sealed class BillingModule : IMvcModule
/// {
///     public void Configure(MvcApplicationBuilder builder) =>
///         builder.Services.AddSingleton&lt;IInvoiceStore, InvoiceStore&gt;();
/// }
///
/// builder.AddModule&lt;BillingModule&gt;(); // in the application's Program.cs
/// </code>
/// </example>
public interface IMvcModule
{
    /// <summary>Registers the module's services and settings; called once, when the module is added.</summary>
    /// <param name="builder">The application's builder.</param>
    void Configure(MvcApplicationBuilder builder);
}
