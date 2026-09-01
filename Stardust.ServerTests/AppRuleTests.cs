using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stardust.Data.Configs;
using Xunit;

namespace ServerTest
{
    public class AppRuleTests
    {
        /// <summary>确保测试规则存在</summary>
        private static void EnsureRule()
        {
            var entity = AppRule.FindById(2) ?? new AppRule();
            entity.Rule = "LocalIP=172.*";
            entity.Result = "Scope=pro";
            entity.Enable = true;
            entity.Save();
        }

        [Fact(DisplayName = "CheckScope_无客户端标识_返回空")]
        public void CheckScope_NoClientId_ReturnsNull()
        {
            EnsureRule();

            var scope = AppRule.CheckScope(1, null, null);
            Assert.Null(scope);
        }

        [Fact(DisplayName = "CheckScope_匹配本地IP规则_返回作用域")]
        public void CheckScope_MatchingRule_ReturnsScope()
        {
            EnsureRule();

            var clientId = "172.21.69.46@3144";
            var scope = AppRule.CheckScope(1, null, clientId);

            Assert.Equal("pro", scope);
        }

        [Fact(DisplayName = "CheckScope_不匹配规则_返回其他作用域")]
        public void CheckScope_NotMatchingRule_ReturnsOtherScope()
        {
            EnsureRule();

            var clientId = "192.168.0.46@3144";
            var scope = AppRule.CheckScope(1, null, clientId);

            Assert.NotEqual("pro", scope);
        }
    }
}
