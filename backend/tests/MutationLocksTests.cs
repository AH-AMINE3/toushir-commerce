using Microsoft.AspNetCore.Http;
using Toushir.Wholesale;
using Xunit;

public sealed class MutationLocksTests
{
    [Fact]
    public void SeparateWorkspacesAndSchemasDoNotShareMutationLocks()
    {
        var first = MutationLocks.Keys("commerce", "first", "/api/orders");
        Assert.Equal(first, MutationLocks.Keys("commerce", "first", "/api/cart"));
        Assert.NotEqual(first[0], MutationLocks.Keys("commerce", "second", "/api/orders")[0]);
        Assert.NotEqual(first[0], MutationLocks.Keys("other", "first", "/api/orders")[0]);
    }

    [Fact]
    public void IdentitySwitchDoesNotQueueBehindItsPreviousWorkspaceHistory()
    {
        var anonymous = MutationLocks.Keys("commerce", null, "/api/demo/session");
        Assert.Equal(anonymous, MutationLocks.Keys("commerce", "previous", "/api/demo/session"));
        Assert.Equal(anonymous, MutationLocks.Keys("commerce", "previous", "/api/auth/login"));
        Assert.Equal(anonymous, MutationLocks.Keys("commerce", "previous", "/api/demo/join"));
        Assert.NotEqual(anonymous[0], MutationLocks.Keys("commerce", "previous", "/api/admin/demo/scenario")[0]);
    }

    [Fact]
    public void CatalogueImportsShareFirstLockAndRetainOwnWorkspaceLock()
    {
        var first = MutationLocks.Keys("commerce", "first", "/api/admin/catalog/import");
        var second = MutationLocks.Keys("commerce", "second", "/api/admin/catalog/import");
        Assert.Equal(2, first.Count);
        Assert.Equal(first[0], second[0]);
        Assert.Equal(MutationLocks.Keys("commerce", "first", "/api/orders")[0], first[1]);
        Assert.NotEqual(first[1], second[1]);
    }
}
