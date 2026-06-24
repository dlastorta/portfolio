using FluentAssertions;
using ModularMonolith.Application;
using ModularMonolith.Domain.Common;
using NetArchTest.Rules;
using Xunit;

namespace ModularMonolith.ArchitectureTests;

/// <summary>
/// These tests turn the layering rules into build-time assertions. Discipline erodes;
/// a failing test in CI does not. This is the "guard the boundaries with a test"
/// recommendation from the writeup, implemented.
/// </summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "ModularMonolith.Domain";
    private const string ApplicationNamespace = "ModularMonolith.Application";
    private const string InfrastructureNamespace = "ModularMonolith.Infrastructure";
    private const string WebApiNamespace = "ModularMonolith.WebApi";

    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, WebApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_external_frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MediatR",
                "Microsoft.EntityFrameworkCore",
                "Riok.Mapperly",
                "FluentValidation",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_webapi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, WebApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Application_should_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    private static string FailureMessage(TestResult result)
    {
        var names = result.FailingTypeNames;
        return names is not null && names.Any()
            ? "offending types: " + string.Join(", ", names)
            : "the layer boundary was violated";
    }
}
