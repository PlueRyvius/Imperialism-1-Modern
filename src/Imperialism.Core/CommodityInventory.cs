namespace Imperialism.Core;

public enum PendingDeliverySource : byte
{
    Transport,
    Trade,
}

/// <summary>An identifiable commodity intent that has not entered available stock.</summary>
public readonly record struct PendingDelivery
{
    public PendingDelivery(
        DeliveryId id,
        CountryId recipient,
        CommodityId commodity,
        long quantity,
        PendingDeliverySource source)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Delivery quantity must be positive.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        Id = id;
        Recipient = recipient;
        Commodity = commodity;
        Quantity = quantity;
        Source = source;
    }

    public DeliveryId Id { get; }

    public CountryId Recipient { get; }

    public CommodityId Commodity { get; }

    public long Quantity { get; }

    public PendingDeliverySource Source { get; }
}
