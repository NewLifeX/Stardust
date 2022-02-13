using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stardust.Data.Configs;
using Xunit;

namespace Stardust.ServerTests
{
    public class AppRuleTests
    {
        [Fact]
        public void Test1()
        {
            var entity = AppRule.FindById(2) ?? new AppRule();

            entity.Rule = "LocalIP=172.*";
            entity.Result = "Scope=pro";
            entity.Enable = true;
            entity.Save();

            var scope = AppRule.CheckScope(1, null, null);
            Assert.Null(scope);

            var clientId = "172.21.69.46@3144";
            scope = AppRule.CheckScope(1, null, clientId);

            Assert.Equal("pro", scope);

            clientId = "192.168.0.46@3144";
            scope = AppRule.CheckScope(1, null, clientId);

            Assert.NotEqual("pro", scope);
        }
    }
}
