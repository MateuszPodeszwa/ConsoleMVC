namespace ConsoleMVC.Mvc;

/// <summary>
/// Marks a view property that the framework fills from the application's services before the view renders.
/// The <c>@inject</c> directive in a <c>.cvw</c> file generates such a property.
/// </summary>
/// <remarks>
/// The property must have a setter (it may be private). When no service of the property's type is registered the
/// view fails with an <see cref="InvalidOperationException"/> naming the view and the property.
/// </remarks>
/// <example>
/// <code>
/// // @inject IGreetingService Greetings   generates:
/// [Inject]
/// public IGreetingService Greetings { get; private set; } = default!;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute
{
}
