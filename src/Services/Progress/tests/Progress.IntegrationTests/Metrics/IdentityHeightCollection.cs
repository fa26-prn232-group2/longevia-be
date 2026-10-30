namespace Progress.IntegrationTests.Metrics;

// Both flows use the same actual provider; serialize profile changes and provider stop/restart tests.
[CollectionDefinition("Actual Identity height")]
public sealed class IdentityHeightCollection : ICollectionFixture<IdentityHeightFixture>;
