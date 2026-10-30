namespace Progress.IntegrationTests.Metrics;

// Keep the listener preflight independent of other fixtures' blocking Docker discovery/startup.
[CollectionDefinition("Weight database fault", DisableParallelization = true)]
public sealed class WeightDatabaseFaultCollection;
