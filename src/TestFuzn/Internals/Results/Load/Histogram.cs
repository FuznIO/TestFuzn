using System.IO.Compression;
using System.Numerics;

namespace Fuzn.TestFuzn.Internals.Results.Load;

// Durations (in ticks) counted in buckets. Below 2048 every value has its own bucket; above it, each
// doubling (2048-4095, 4096-8191, ...) has 1024 buckets, so a value is off by less than 0.1 %. The buckets
// are kept in groups of 1024 (8 KB), and a group exists only once a time falls in it: the size follows the
// spread of the times, not their number or the run length, and there is no longest time.
// Not thread-safe; the collectors lock around it.
internal sealed class Histogram
{
    private const int SubBucketBits = 10;
    private const int SubBuckets = 1 << SubBucketBits;
    private const long ExactBelow = 2 * SubBuckets;

    // About 14 600 years in ticks; larger values count as this. Keeps the bucket math clear of overflow.
    private const long HighestValue = (1L << 62) - 1;
    private static readonly int HighestIndex = IndexOf(HighestValue);

    private readonly long[]?[] _groups = new long[]?[(HighestIndex >> SubBucketBits) + 1];

    public long TotalCount { get; private set; }

    public void Record(long value, long count = 1)
    {
        AddAt(IndexOf(value), count);
    }

    public void Add(Histogram other)
    {
        foreach (var (index, count) in other.Buckets())
            AddAt(index, count);
    }

    // Both histograms are cumulative; the result is what was recorded in between.
    public static Histogram Subtract(Histogram current, Histogram? previous)
    {
        var difference = new Histogram();
        foreach (var (index, count) in current.Buckets())
        {
            var before = previous != null ? previous.CountAt(index) : 0;
            difference.AddAt(index, count - before);
        }

        return difference;
    }

    // The highest value of the bucket that holds the percentile, the same rule as HdrHistogram.
    public long ValueAtPercentile(double percentile)
    {
        if (TotalCount == 0)
            return 0;

        var countAtPercentile = (long) (Math.Clamp(percentile, 0, 100) / 100 * TotalCount + 0.5);
        countAtPercentile = Math.Max(countAtPercentile, 1);

        var seen = 0L;
        foreach (var (index, count) in Buckets())
        {
            seen += count;
            if (seen >= countAtPercentile)
                return HighestAt(index);
        }

        return MaxValue();
    }

    public long MaxValue()
    {
        for (var group = _groups.Length - 1; group >= 0; group--)
        {
            var counts = _groups[group];
            if (counts == null)
                continue;

            for (var offset = SubBuckets - 1; offset >= 0; offset--)
            {
                if (counts[offset] > 0)
                    return HighestAt((group << SubBucketBits) + offset);
            }
        }

        return 0;
    }

    // Each bucket counts as its middle value.
    public double StandardDeviation()
    {
        if (TotalCount == 0)
            return 0;

        var sum = 0.0;
        foreach (var (index, count) in Buckets())
            sum += Middle(index) * count;
        var mean = sum / TotalCount;

        var squares = 0.0;
        foreach (var (index, count) in Buckets())
        {
            var deviation = Middle(index) - mean;
            squares += deviation * deviation * count;
        }

        return Math.Sqrt(squares / TotalCount);
    }

    // The buckets with a count as (bucket delta, count) varint pairs, deflated and base64.
    public string Encode()
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            var previous = 0;
            foreach (var (index, count) in Buckets())
            {
                WriteVarint(deflate, index - previous);
                WriteVarint(deflate, count);
                previous = index;
            }
        }

        return Convert.ToBase64String(output.ToArray());
    }

    public static Histogram Decode(string encoded)
    {
        var histogram = new Histogram();
        using var input = new DeflateStream(new MemoryStream(Convert.FromBase64String(encoded)), CompressionMode.Decompress);

        var index = 0L;
        while (TryReadVarint(input, out var delta))
        {
            if (!TryReadVarint(input, out var count))
                throw new InvalidDataException("The encoded histogram ends in the middle of a bucket.");

            index += delta;
            if (delta < 0 || index > HighestIndex || count < 0)
                throw new InvalidDataException("The encoded histogram has a bucket out of range.");

            histogram.AddAt((int) index, count);
        }

        return histogram;
    }

    internal static int IndexOf(long value)
    {
        value = Math.Clamp(value, 0, HighestValue);
        if (value < ExactBelow)
            return (int) value;

        var shift = BitOperations.Log2((ulong) value) - SubBucketBits;
        return (shift << SubBucketBits) + (int) (value >> shift);
    }

    internal static long LowestAt(int index)
    {
        if (index < ExactBelow)
            return index;

        var shift = (index >> SubBucketBits) - 1;
        return (long) (index - (shift << SubBucketBits)) << shift;
    }

    internal static long HighestAt(int index)
    {
        return LowestAt(index + 1) - 1;
    }

    private static double Middle(int index)
    {
        return (LowestAt(index) + (double) HighestAt(index)) / 2;
    }

    private void AddAt(int index, long count)
    {
        if (count <= 0)
            return;

        var counts = _groups[index >> SubBucketBits] ??= new long[SubBuckets];
        counts[index & (SubBuckets - 1)] += count;
        TotalCount += count;
    }

    private long CountAt(int index)
    {
        var counts = _groups[index >> SubBucketBits];
        return counts != null ? counts[index & (SubBuckets - 1)] : 0;
    }

    public IEnumerable<(long Low, long High, long Count)> Ranges()
    {
        foreach (var (index, count) in Buckets())
            yield return (LowestAt(index), HighestAt(index), count);
    }

    // The buckets with a count, from the fastest to the slowest.
    private IEnumerable<(int Index, long Count)> Buckets()
    {
        for (var group = 0; group < _groups.Length; group++)
        {
            var counts = _groups[group];
            if (counts == null)
                continue;

            for (var offset = 0; offset < SubBuckets; offset++)
            {
                if (counts[offset] > 0)
                    yield return ((group << SubBucketBits) + offset, counts[offset]);
            }
        }
    }

    private static void WriteVarint(Stream stream, long value)
    {
        var remaining = (ulong) value;
        while (remaining >= 0x80)
        {
            stream.WriteByte((byte) (remaining | 0x80));
            remaining >>= 7;
        }

        stream.WriteByte((byte) remaining);
    }

    private static bool TryReadVarint(Stream stream, out long value)
    {
        value = 0;
        var shift = 0;
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0)
            {
                if (shift == 0)
                    return false;

                throw new InvalidDataException("The encoded histogram ends in the middle of a number.");
            }

            if (shift > 63)
                throw new InvalidDataException("The encoded histogram has a number that is too long.");

            value |= (long) (next & 0x7F) << shift;
            if ((next & 0x80) == 0)
                return true;

            shift += 7;
        }
    }
}
