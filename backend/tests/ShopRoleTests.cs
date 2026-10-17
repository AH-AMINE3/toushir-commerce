using Xunit;
namespace Toushir.Wholesale.Tests;
public class ShopRoleTests
{
    [Theory]
    [InlineData("OWNER",true,true)]
    [InlineData("MANAGER",true,true)]
    [InlineData("BUYER",true,false)]
    [InlineData("VIEWER",false,false)]
    [InlineData("ADMIN",false,false)]
    [InlineData("",false,false)]
    public void Shop_authority_is_explicit_and_independent_from_the_wholesale_role(string role,bool order,bool manage)
    {
        var actor=new Actor("person","workspace","Person","MERCHANT","APPROVED",false,"shop",role,3);
        Assert.Equal("person",actor.Id);Assert.Equal("shop",actor.ResourceOwnerId);
        Assert.Equal(order,actor.CanShopWrite);Assert.Equal(manage,actor.CanManageShop);Assert.False(actor.IsAdmin);
    }
    [Theory]
    [InlineData("ADMIN")][InlineData("WAREHOUSE")][InlineData("FINANCE")][InlineData("DRIVER")][InlineData("DISPATCHER")][InlineData("SALES")]
    public void A_shop_label_cannot_grant_a_staff_account_merchant_authority(string role)
    {
        var actor=new Actor("person","workspace","Person",role,"APPROVED",false,"shop","MANAGER",4);
        Assert.False(actor.CanShopWrite);Assert.False(actor.CanManageShop);Assert.Equal("person",actor.ResourceOwnerId);
    }
    [Fact]
    public void The_primary_account_remains_the_legacy_shop_owner()
    {
        var actor=new Actor("legacy-shop","workspace","Owner","MERCHANT","APPROVED");
        Assert.Equal(actor.Id,actor.ResourceOwnerId);Assert.False(actor.IsShopMember);Assert.True(actor.CanManageShop);
    }
    [Fact]
    public void A_commercial_portfolio_does_not_grant_the_operational_live_audience()
    {
        var actor=new Actor("adviser","workspace","Adviser","SALES","APPROVED");
        Assert.False(actor.IsStaff);Assert.False(actor.IsAdmin);Assert.False(actor.CanShopWrite);Assert.False(actor.CanManageShop);
    }
}
