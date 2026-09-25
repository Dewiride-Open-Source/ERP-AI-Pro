using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class RequestRecordTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string RequestsNamespaceSuffix = ".Endpoints.Requests";

    private static readonly Type[] RequestRecords = ErpAssemblies.ModuleImplementations
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type.Namespace is { } ns && ns.EndsWith(RequestsNamespaceSuffix, StringComparison.Ordinal))
        .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        .ToArray();

    [Fact]
    public void RequestRecords_Always_ArePublicSealedRecords()
    {
        var violations = RequestRecords
            .Where(type => !type.IsPublic || !type.IsSealed || type.GetMethod("<Clone>$") is null)
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void RequestRecords_Always_TargetTheirAttributesAtTheProperty()
    {
        var violations = RequestRecords
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()))
            .SelectMany(parameter => parameter.GetCustomAttributes().Where(IsContractAttribute).Select(attribute => $"{parameter.Member.DeclaringType!.FullName}({parameter.Name}): [{attribute.GetType().Name}]"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void RequestRecords_WithValidationRules_AreKnownToTheValidationPipeline()
    {
        var options = factory.Services.GetRequiredService<IOptions<ValidationOptions>>().Value;

        var unknown = RequestRecords
            .Where(HasValidationRules)
#pragma warning disable ASP0029 // TryGetValidatableTypeInfo is marked experimental in .NET 10; it is the only way to ask which types the generated resolvers cover.
            .Where(type => !options.TryGetValidatableTypeInfo(type, out _))
#pragma warning restore ASP0029
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(unknown);
    }

    private static bool HasValidationRules(Type type) =>
        typeof(IValidatableObject).IsAssignableFrom(type)
        || type.GetProperties().Any(property => property.IsDefined(typeof(ValidationAttribute), inherit: true));

    private static bool IsContractAttribute(Attribute attribute) =>
        attribute is ValidationAttribute or DescriptionAttribute
        || attribute.GetType().Namespace?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true;
}
