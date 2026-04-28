using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web;
using Xunit;

namespace ClientTest;

public class TokenModelTests
{
    [Fact]
    public void Test1()
    {
        var model = new TokenModel
        {
            AccessToken = Rand.NextString(32),
            TokenType = "token",
            ExpireIn = 7200,
            RefreshToken = Rand.NextString(32),
        };

        var json = model.ToJson();

        var rs = "{\"access_token\":\"{access_token}\",\"token_type\":\"token\",\"expire_in\":7200,\"refresh_token\":\"{refresh_token}\"}";
        rs = rs.Replace("{access_token}", model.AccessToken)
            .Replace("{refresh_token}", model.RefreshToken);

        Assert.Equal(rs, json);
    }
}
