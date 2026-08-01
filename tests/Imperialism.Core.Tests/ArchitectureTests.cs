using System.Reflection;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreAssemblyDoesNotReferenceLegacyFormatsOrGodot()
    {
        var references = typeof(WorldDefinition).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, static reference =>
            reference.Name?.StartsWith("Godot", StringComparison.Ordinal) == true ||
            reference.Name == "Imperialism.Formats");
    }

    [Fact]
    public void CorePublicApiAvoidsIoRandomAndFloatingPointTypes()
    {
        var forbidden = typeof(WorldDefinition).Assembly
            .GetExportedTypes()
            .SelectMany(PublicApiTypes)
            .Where(IsForbidden)
            .Distinct()
            .ToArray();

        Assert.Empty(forbidden);
    }

    private static IEnumerable<Type> PublicApiTypes(Type type)
    {
        yield return type;
        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var property in type.GetProperties())
        {
            yield return property.PropertyType;
        }

        foreach (var method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static bool IsForbidden(Type type)
    {
        if (type.IsArray || type.IsByRef || type.IsPointer)
        {
            return IsForbidden(type.GetElementType()!);
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(IsForbidden))
        {
            return true;
        }

        return type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(Random) ||
            type.Namespace?.StartsWith("System.IO", StringComparison.Ordinal) == true ||
            type.Namespace?.StartsWith("Godot", StringComparison.Ordinal) == true;
    }
}
