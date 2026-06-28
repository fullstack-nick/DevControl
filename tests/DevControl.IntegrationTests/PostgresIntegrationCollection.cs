using Xunit;

namespace DevControl.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIntegrationCollection
{
    public const string Name = "PostgresIntegration";
}
