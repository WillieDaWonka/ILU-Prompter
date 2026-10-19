using IluPrompter.Core;

namespace IluPrompter.Input;

/// <summary>A supported Contour model and how its raw button bits map to logical button numbers.</summary>
internal sealed class ContourModel
{
    private readonly int[] _bitToLogical;

    public ContourModel(int vendorId, int productId, string name, int buttonCount, int[] bitToLogical)
    {
        VendorId = vendorId;
        ProductId = productId;
        Name = name;
        ButtonCount = buttonCount;
        _bitToLogical = bitToLogical;
    }

    public int VendorId { get; }

    public int ProductId { get; }

    public string Name { get; }

    public int ButtonCount { get; }

    /// <summary>Logical (1-based, left to right, top to bottom) number of a raw bit; bits the model does not
    /// know pass through as <c>bit + 1</c>.</summary>
    public int LogicalButton(int bit) =>
        bit >= 0 && bit < _bitToLogical.Length && _bitToLogical[bit] > 0 ? _bitToLogical[bit] : bit + 1;

    public string ControlId(int bit) => ControlIds.ShuttleButton(LogicalButton(bit));
}

/// <summary>
/// The supported models. VERIFY the button maps on the physical devices (Phase-0): they come from the
/// LinuxCNC <c>shuttle.c</c> description of the report, not from a unit in hand.
/// </summary>
internal static class ContourModels
{
    public const int ContourVendorId = 0x0B33;

    /// <summary>ShuttleXpress: five buttons at raw bits 4..8, numbered 1..5 left to right.</summary>
    public static ContourModel ShuttleXpress { get; } = new(
        ContourVendorId, 0x0020, "ShuttleXpress", 5, Map((4, 1), (5, 2), (6, 3), (7, 4), (8, 5)));

    /// <summary>ShuttlePRO v2: fifteen buttons at raw bits 0..14, numbered 1..15.</summary>
    public static ContourModel ShuttleProV2 { get; } = new(
        ContourVendorId, 0x0030, "ShuttlePRO v2", 15, Identity(15));

    /// <summary>ShuttlePRO v1 (Contour's earlier vendor id): same layout as v2.</summary>
    public static ContourModel ShuttleProV1 { get; } = new(
        0x05F3, 0x0240, "ShuttlePRO", 15, Identity(15));

    public static IReadOnlyList<ContourModel> All { get; } = new[] { ShuttleXpress, ShuttleProV2, ShuttleProV1 };

    public static bool TryGet(int vendorId, int productId, out ContourModel model)
    {
        foreach (var m in All)
        {
            if (m.VendorId == vendorId && m.ProductId == productId)
            {
                model = m;
                return true;
            }
        }

        model = null!;
        return false;
    }

    private static int[] Identity(int count)
    {
        var map = new int[16];
        for (var bit = 0; bit < count; bit++)
        {
            map[bit] = bit + 1;
        }

        return map;
    }

    private static int[] Map(params (int Bit, int Logical)[] pairs)
    {
        var map = new int[16];
        foreach (var (bit, logical) in pairs)
        {
            map[bit] = logical;
        }

        return map;
    }
}
