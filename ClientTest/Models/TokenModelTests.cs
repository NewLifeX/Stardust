using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web;
using System;
using System.Collections.Generic;
using Xunit;

namespace ClientTest.Models;

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
        var dic = json.ToJsonEntity<Dictionary<String, Object>>();

        Assert.Equal(model.AccessToken, dic["access_token"] + "");
        Assert.Equal(model.TokenType, dic["token_type"] + "");
        Assert.Equal(model.ExpireIn, Int32.Parse(dic["expire_in"] + ""));
        Assert.Equal(model.RefreshToken, dic["refresh_token"] + "");
    }
}
