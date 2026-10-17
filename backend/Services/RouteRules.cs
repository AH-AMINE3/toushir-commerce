namespace Toushir.Wholesale;

public sealed record RouteLoad(long PaidUnits,long GiftUnits,long TotalUnits,int? CapacityUnits,long? RemainingUnits,string Unit="COMMERCIAL_UNIT");

/// <summary>Explicit commercial-unit capacity and physical cash invariants; no weight, merchant debt or external settlement is inferred.</summary>
public static class RouteRules
{
    public static RouteLoad Load(IEnumerable<long> paid,IEnumerable<long> gifts,int? capacity)
    {
        var paidUnits=paid.Sum();var giftUnits=gifts.Sum();
        Check.That(paidUnits>=0&&giftUnits>=0,"La charge doit être positive.");
        var total=checked(paidUnits+giftUnits);
        return new(paidUnits,giftUnits,total,capacity,capacity-total);
    }
    public static RouteLoad RequireCapacity(IEnumerable<long> paid,IEnumerable<long> gifts,int? capacity)
    {
        Check.That(capacity is >0 and <=1000000,"Confirmez une capacité de 1 à 1 000 000 unités commerciales.","CAPACITY_REQUIRED");
        var load=Load(paid,gifts,capacity);
        Check.That(load.TotalUnits<=capacity,"La charge du manifeste, gratuités comprises, dépasse la capacité confirmée du véhicule.","ROUTE_OVER_CAPACITY",409);
        return load;
    }
    public static void RequireVersion(int current,int submitted)=>Check.That(current==submitted,"Le manifeste de tournée a changé. Rechargez-le.","ROUTE_VERSION_CONFLICT",409);
    public static decimal ExpectedCash(decimal openingFloat,IEnumerable<(decimal Tendered,decimal Change)> receipts)
    {
        Check.Money(openingFloat,"Fond de caisse");var expected=openingFloat;
        foreach(var receipt in receipts){Check.Money(receipt.Tendered,"Espèces reçues");Check.Money(receipt.Change,"Monnaie rendue");Check.That(receipt.Change<=receipt.Tendered,"La monnaie rendue dépasse les espèces reçues.","CASH_MISMATCH",409);expected=checked(expected+receipt.Tendered-receipt.Change);}
        return expected;
    }
    public static (decimal Shortage,decimal Surplus) Discrepancy(decimal expected,decimal counted){Check.Money(counted,"Comptage");return(Math.Max(0,expected-counted),Math.Max(0,counted-expected));}
}
