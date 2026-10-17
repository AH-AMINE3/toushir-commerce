using Toushir.Wholesale;
using Xunit;

public sealed class RouteRulesTests
{
    [Fact] public void GiftsCountTowardCapacity(){var load=RouteRules.RequireCapacity([4,3],[2],9);Assert.Equal(9,load.TotalUnits);Assert.Equal(0,load.RemainingUnits);}
    [Fact] public void GiftOverloadIsRejectedBeforeCustody(){var error=Assert.Throws<ApiFailure>(()=>RouteRules.RequireCapacity([4,3],[2],8));Assert.Equal("ROUTE_OVER_CAPACITY",error.Code);Assert.Equal(409,error.Status);}
    [Theory][InlineData(null)][InlineData(0)][InlineData(-1)][InlineData(1000001)]public void UnknownOrInvalidCapacityCannotAuthorizeDeparture(int? capacity)=>Assert.Equal("CAPACITY_REQUIRED",Assert.Throws<ApiFailure>(()=>RouteRules.RequireCapacity([1],[],capacity)).Code);
    [Fact] public void LoadCountsDoNotOverflow32BitBeforeRejection()=>Assert.Equal("ROUTE_OVER_CAPACITY",Assert.Throws<ApiFailure>(()=>RouteRules.RequireCapacity([int.MaxValue,int.MaxValue],[1],100)).Code);
    [Fact] public void StaleManifestVersionIsRejected()=>Assert.Equal("ROUTE_VERSION_CONFLICT",Assert.Throws<ApiFailure>(()=>RouteRules.RequireVersion(5,4)).Code);
    [Fact] public void ExactCurrentManifestVersionIsAccepted()=>RouteRules.RequireVersion(5,5);
    [Fact] public void FigmaCashFixtureCountsFloatAndNetTender(){var expected=RouteRules.ExpectedCash(5000m,[(19400m,100m),(21200m,100m),(18700m,100m)]);Assert.Equal(64000m,expected);Assert.Equal((500m,0m),RouteRules.Discrepancy(expected,63500m));}
    [Fact] public void SurplusAndShortageStayIndependent(){Assert.Equal((0m,50.25m),RouteRules.Discrepancy(100m,150.25m));Assert.Equal((50.25m,0m),RouteRules.Discrepancy(150.25m,100m));}
    [Fact] public void DecimalCashIsExact()=>Assert.Equal(.30m,RouteRules.ExpectedCash(.10m,[(.30m,.10m)]));
    [Fact] public void CashChangeCannotExceedTender()=>Assert.Equal("CASH_MISMATCH",Assert.Throws<ApiFailure>(()=>RouteRules.ExpectedCash(0m,[(10m,11m)])).Code);
    [Fact] public void FractionalCentimeCannotEnterCashLedger()=>Assert.Throws<ApiFailure>(()=>RouteRules.ExpectedCash(.001m,[]));
    [Theory][InlineData("public")][InlineData("pg_catalog")][InlineData("BadSchema")][InlineData("commerce; DROP SCHEMA public")][InlineData("commerce,public")]
    public void SharedDatabaseSchemaCannotEscape(string schema)=>Assert.Throws<InvalidOperationException>(()=>DatabaseConfiguration.ValidateSchema(schema));
    [Fact] public void DedicatedSchemaAcceptsStableLowercaseIdentifier()=>Assert.Equal("commerce_ecosystem",DatabaseConfiguration.ValidateSchema("commerce_ecosystem"));
    [Fact] public void PostgresCredentialsAndDedicatedSearchPathCannotInjectSettings(){var value=DatabaseConfiguration.PostgresConnectionString("postgresql://operator:a%3Bb%3Dc%3Ad@localhost:5432/test","commerce_ecosystem");var parsed=new Npgsql.NpgsqlConnectionStringBuilder(value);Assert.Equal("a;b=c:d",parsed.Password);Assert.Equal("commerce_ecosystem",parsed.SearchPath);Assert.Equal(20,parsed.MaxPoolSize);}
    [Fact] public void PaymentStateIsIndependentFromDelivery(){var order=new PurchaseOrder{Status="DELIVERED",Total=18600m,Collected=18600m};OrderService.UpdatePayment(order);Assert.Equal("COLLECTED",order.PaymentStatus);var discrepancy=RouteRules.Discrepancy(64000m,63500m);Assert.Equal(500m,discrepancy.Shortage);Assert.Equal(18600m,order.Collected);Assert.Equal(18600m,order.Total);}
}
