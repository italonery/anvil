using Anvil.Http.Resilience;
using Anvil.Messaging.Centrifugo;
using Anvil.Persistence.Postgres;
using Anvil.Primitives;
using Anvil.Scheduling.Quartz;
using Anvil.Security;
using NetArchTest.Rules;
using Xunit;

namespace Anvil.ArchitectureTests;

public sealed class ArchitectureFitnessTests
{
    [Fact]
    public void Primitives_Should_Not_Have_Any_Infrastructure_Dependencies()
    {
        var result = Types.InAssembly(typeof(Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Anvil.Security",
                "Anvil.Persistence.Postgres",
                "Anvil.Scheduling.Quartz",
                "Anvil.Messaging.Centrifugo",
                "Anvil.Http.Resilience",
                "Microsoft.EntityFrameworkCore",
                "Quartz",
                "Grpc.Core")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_Concrete_Classes_Should_Be_Sealed()
    {
        var assemblies = new[]
        {
            typeof(CryptoUtils).Assembly,
            typeof(EfDbContextFactory).Assembly,
            typeof(QuartzSchedulerManager).Assembly,
            typeof(CentrifugoPublisher).Assembly,
            typeof(RestClient).Assembly
        };

        var result = Types.InAssemblies(assemblies)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveNameStartingWith("<>")
            .And()
            .DoNotResideInNamespace("Centrifugal.Centrifugo.Api")
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
