namespace FiguritasApi.Tests;

public class UnitTest1
{
    [Fact]
    public void TestUserCreation()
    {
        var user = new FiguritasApi.Model.User { Username = "test" };
        Assert.NotNull(user);
        Assert.Equal("test", user.Username);
    }
}
