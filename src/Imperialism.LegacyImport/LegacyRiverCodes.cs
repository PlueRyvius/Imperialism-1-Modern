using Imperialism.Core;

namespace Imperialism.LegacyImport;

public static class LegacyRiverCodes
{
    private static readonly IReadOnlyDictionary<byte, RiverPath> Paths =
        new Dictionary<byte, RiverPath>
        {
            [11] = Path(RiverEndpoint.NorthEast, RiverEndpoint.SouthEast),
            [12] = Path(RiverEndpoint.NorthEast, RiverEndpoint.SouthWest),
            [13] = Path(RiverEndpoint.NorthEast, RiverEndpoint.WestUpper),
            [14] = Path(RiverEndpoint.NorthEast, RiverEndpoint.WestLower),
            [15] = Path(RiverEndpoint.SouthWest, RiverEndpoint.EastUpper),
            [16] = Path(RiverEndpoint.SouthWest, RiverEndpoint.EastLower),
            [17] = Path(RiverEndpoint.EastUpper, RiverEndpoint.WestUpper),
            [18] = Path(RiverEndpoint.EastLower, RiverEndpoint.WestUpper),
            [19] = Path(RiverEndpoint.EastUpper, RiverEndpoint.WestLower),
            [20] = Path(RiverEndpoint.EastLower, RiverEndpoint.WestLower),
            [21] = Path(RiverEndpoint.EastUpper, RiverEndpoint.NorthWest),
            [22] = Path(RiverEndpoint.EastLower, RiverEndpoint.NorthWest),
            [23] = Path(RiverEndpoint.SouthEast, RiverEndpoint.WestUpper),
            [24] = Path(RiverEndpoint.SouthEast, RiverEndpoint.WestLower),
            [25] = Path(RiverEndpoint.SouthEast, RiverEndpoint.NorthWest),
            [26] = Path(RiverEndpoint.SouthWest, RiverEndpoint.NorthWest),
            [43] = Path(RiverEndpoint.NorthEast, RiverEndpoint.Source),
            [44] = Path(RiverEndpoint.EastUpper, RiverEndpoint.Source),
            [45] = Path(RiverEndpoint.EastLower, RiverEndpoint.Source),
            [46] = Path(RiverEndpoint.SouthEast, RiverEndpoint.Source),
            [47] = Path(RiverEndpoint.SouthWest, RiverEndpoint.Source),
            [48] = Path(RiverEndpoint.WestUpper, RiverEndpoint.Source),
            [49] = Path(RiverEndpoint.WestLower, RiverEndpoint.Source),
            [50] = Path(RiverEndpoint.NorthWest, RiverEndpoint.Source),
            [51] = Path(RiverEndpoint.NorthEast, RiverEndpoint.Mouth),
            [52] = Path(RiverEndpoint.EastUpper, RiverEndpoint.Mouth),
            [53] = Path(RiverEndpoint.EastLower, RiverEndpoint.Mouth),
            [54] = Path(RiverEndpoint.SouthEast, RiverEndpoint.Mouth),
            [55] = Path(RiverEndpoint.SouthWest, RiverEndpoint.Mouth),
            [56] = Path(RiverEndpoint.WestUpper, RiverEndpoint.Mouth),
            [57] = Path(RiverEndpoint.WestLower, RiverEndpoint.Mouth),
            [58] = Path(RiverEndpoint.NorthWest, RiverEndpoint.Mouth),
        };

    public static IReadOnlyDictionary<byte, RiverPath> KnownPaths => Paths;

    public static bool TryDecode(byte code, out RiverPath path) => Paths.TryGetValue(code, out path);

    private static RiverPath Path(RiverEndpoint first, RiverEndpoint second) => new(first, second);
}
