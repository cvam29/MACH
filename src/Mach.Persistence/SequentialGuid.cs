namespace Mach.Persistence;

/// <summary>
/// Generates GUIDs that sort sequentially under SQL Server's <c>uniqueidentifier</c>
/// ordering, reducing index fragmentation for clustered GUID primary keys.
/// </summary>
internal static class SequentialGuid
{
    public static Guid NewGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        Guid.NewGuid().TryWriteBytes(bytes);

        // SQL Server orders the last 6 bytes most significantly; stamp them with a
        // monotonically increasing timestamp so inserts append to the index.
        long ticks = DateTime.UtcNow.Ticks;
        Span<byte> ticksBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(ticksBytes, ticks);

        // SQL Server's byte significance for the final 8 bytes: [8][9] then [10..15].
        bytes[10] = ticksBytes[5];
        bytes[11] = ticksBytes[4];
        bytes[12] = ticksBytes[3];
        bytes[13] = ticksBytes[2];
        bytes[14] = ticksBytes[1];
        bytes[15] = ticksBytes[0];

        return new Guid(bytes);
    }
}
