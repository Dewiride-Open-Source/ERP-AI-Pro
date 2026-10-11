namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Persistence;

// A test that holds the organisation lock makes every other write to departments, designations and positions of the test
// database wait for it and then fail, so its tests run alone, after the collections that run in parallel.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OrganisationLockHolders
{
    public const string Name = "Organisation lock holders";
}
