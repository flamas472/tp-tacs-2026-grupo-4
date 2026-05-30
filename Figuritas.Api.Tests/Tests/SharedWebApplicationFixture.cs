using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Shared xUnit collection fixture to prevent parallel instantiation of
/// WebApplicationFactory, which would cause MongoDB BsonClassMap race conditions.
/// All integration test classes should use [Collection(nameof(IntegrationTestCollection))].
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<WebApplicationFactory<Program>>
{
}
