using System.Reflection;
using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// An <c>@inject</c> property may not be named after a member the view already has. The names are read from the real
/// base classes, so a member added to them later is covered without changing this test.
/// </summary>
public class InjectReservedNameTests
{
    /// <summary>The base classes a generated view, partial view or layout derives from.</summary>
    private static readonly Type[] BaseClasses = [typeof(ConsoleView<object>), typeof(ConsolePartialView<object>), typeof(ConsoleLayoutView)];

    /// <summary>Every member of the base classes (and of <see cref="object"/>) that a view in another assembly can see.</summary>
    public static TheoryData<string> MembersOfTheViewBaseClasses()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal) { "Model" }; // Render's parameter
        foreach (var start in BaseClasses)
        {
            for (var type = start; type is not null; type = type.BaseType)
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var member in type.GetMembers(all))
                {
                    if (IsVisibleToDerivedClasses(member) && !member.Name.Contains('<') && !member.Name.Contains('.') && member.Name != ".ctor")
                        names.Add(member.Name);
                }
            }
        }

        var data = new TheoryData<string>();
        foreach (var name in names)
            data.Add(name);
        return data;
    }

    private static bool IsVisibleToDerivedClasses(MemberInfo member) => member switch
    {
        MethodBase method => !method.IsSpecialName && !method.IsConstructor && Visible(method),
        PropertyInfo property => property.GetAccessors(nonPublic: true).Any(Visible),
        EventInfo @event => @event.AddMethod is { } add && Visible(add),
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        Type nested => nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem,
        _ => false
    };

    private static bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    [Theory]
    [MemberData(nameof(MembersOfTheViewBaseClasses))]
    public void InjectNamedAfterAMemberOfTheView_IsCVW007_AtTheName(string name)
    {
        var cvw = $"@inject System.IServiceProvider {name}\n<text>x</text>\nreturn NavigationResult.Quit();";
        var result = ViewHarness.Generate(cvw);

        var error = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW007");
        Assert.Equal((0, cvw.IndexOf(" " + name, StringComparison.Ordinal) + 1),
            (error.Location.GetLineSpan().StartLinePosition.Line, error.Location.GetLineSpan().StartLinePosition.Character));
        Assert.Null(result.GeneratedSource);
        Assert.DoesNotContain(result.CompilationDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [MemberData(nameof(MembersOfTheViewBaseClasses))]
    public void InjectNamedAfterAMemberOfTheView_IsCVW007_InAPartialView(string name)
    {
        var cvw = $"@inject System.IServiceProvider {name}\n<text>x</text>";
        var result = ViewHarness.Generate(cvw, "/app/Views/Shared/Header.cvw");

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW007");
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void InjectNamedAfterTheViewItself_IsCVW007()
    {
        var result = ViewHarness.Generate("@inject System.IServiceProvider IndexView\n<text>x</text>\nreturn NavigationResult.Quit();");

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW007" && d.GetMessage().Contains("IndexView"));
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void InjectWithAnOrdinaryName_StillWorks() =>
        Assert.Empty(ViewHarness.Generate("@inject System.IServiceProvider Provider\n<text>x</text>\nreturn NavigationResult.Quit();").Errors);
}
