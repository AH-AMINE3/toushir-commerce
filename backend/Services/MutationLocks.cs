using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Toushir.Wholesale;

public static class MutationLocks
{
    public static IReadOnlyList<long> Keys(string schema, string? workspace, PathString path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        // Sign-in/demo endpoints may carry a previous actor's cookie while
        // selecting a different account. Never infer their target from it.
        var identity = path.StartsWithSegments("/api/auth") || path.StartsWithSegments("/api/demo") || string.IsNullOrEmpty(workspace);
        var scope = identity ? "identity" : "workspace:" + workspace;
        var keys = new List<long>();
        // Imports change the shared reference catalogue as well as own offers.
        // Always acquire this shared lock before the workspace lock.
        if (path == "/api/admin/catalog/import") keys.Add(Key(schema, "catalogue"));
        keys.Add(Key(schema, scope));
        return keys;
    }

    private static long Key(string schema, string scope) => BinaryPrimitives.ReadInt64BigEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes("Toushir.Wholesale\0" + schema + "\0" + scope)));
}
