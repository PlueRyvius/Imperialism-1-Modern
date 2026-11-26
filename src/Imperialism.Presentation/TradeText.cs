using System.Globalization;

namespace Imperialism.Presentation;

/// <summary>The Trade screen's header, built here so the screen adds no numbers itself.</summary>
public static class TradeText
{
    /// <summary>
    /// Holds and cash, then the bid count against the manual's limit and both drafted
    /// totals at world prices: what the bids would cost and what the offers would fetch.
    /// </summary>
    public static string Header(TradePreview preview) =>
        $"Merchant-marine holds: {Plural.Number(preview.MerchantMarine)} · cash: {Money(preview.Cash)}\n" +
        $"Bids: {preview.BidCount} / {OrdersPreview.MaximumBids} commodities · " +
        $"bids: {Money(preview.TotalBidCost)} · offers: {Money(preview.TotalOfferValue)} at world prices";

    private static string Money(long amount) => "$" + amount.ToString("N0", CultureInfo.InvariantCulture);
}
