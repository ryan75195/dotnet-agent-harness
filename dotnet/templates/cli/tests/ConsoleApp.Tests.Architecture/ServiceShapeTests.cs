using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp.Tests.Architecture;

[TestFixture]
public class ServiceShapeTests
{
    private static readonly HashSet<Type> AllowedConcreteParams =
    [
        typeof(string),
        typeof(System.IO.StreamReader),
        typeof(System.IO.StreamWriter),
        typeof(HttpClient),
        typeof(TimeProvider),
    ];

    [Test]
    public void Should_require_interfaces_on_core_service_classes()
    {
        var serviceTypes = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && TestHelpers.ServiceNamespaces.Contains(t.Namespace)
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t))
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var type in serviceTypes)
            {
                var baseInterfaces = type.BaseType?.GetInterfaces() ?? [];
                var ownInterfaces = type.GetInterfaces().Except(baseInterfaces);

                ownInterfaces.Should().NotBeEmpty(
                    $"{type.Name} must implement an interface (Dependency Inversion Principle)");
            }
        });
    }

    [Test]
    public void Should_inject_dependencies_as_interfaces_not_concrete_types()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.CliAssembly };

        var serviceClasses = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t)
                && t.Namespace?.Contains(".Entities", StringComparison.Ordinal) != true)
            .ToList();

        var violations = new List<string>();

        foreach (var type in serviceClasses)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    var paramType = param.ParameterType;

                    if (paramType.IsInterface)
                    {
                        continue;
                    }

                    if (paramType.IsValueType)
                    {
                        continue;
                    }

                    if (AllowedConcreteParams.Contains(paramType))
                    {
                        continue;
                    }

                    var ns = paramType.Namespace ?? "";
                    if (ns.StartsWith("System.Threading", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (typeof(DbContext).IsAssignableFrom(paramType))
                    {
                        continue;
                    }

                    if (typeof(Delegate).IsAssignableFrom(paramType))
                    {
                        continue;
                    }

                    if (TestHelpers.IsRecord(paramType))
                    {
                        continue;
                    }

                    violations.Add(
                        $"  {type.Name}(… {paramType.Name} {param.Name} …) — should be an interface");
                }
            }
        }

        violations.Should().BeEmpty(
            $"Constructor dependencies must be interfaces, not concrete types. " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_not_allow_static_classes_in_commands_namespace()
    {
        var commandTypes = TestHelpers.CliAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && t.Namespace == "ConsoleApp.Cli.Commands")
            .ToList();

        var violations = commandTypes
            .Where(t => t.IsAbstract && t.IsSealed)
            .Select(t => t.Name)
            .ToList();

        violations.Should().BeEmpty(
            $"Command classes must not be static — use constructor injection. " +
            $"Violations: {string.Join(", ", violations)}");
    }
}
