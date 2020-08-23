using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stardust.Data;
using Stardust.Server.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stardust.Server.Services.Tests
{
    [TestClass()]
    public class AppServiceTests
    {
        [TestMethod()]
        public void AuthorizeTest()
        {
            var app = App.FindByName("test");
            if (app != null) app.Delete();

            var service = new AppService();

            // 没有自动注册
            var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.Authorize("test", "xxx", false));
            Assert.IsNotNull(ex);

            // 启用
            app = App.FindByName("test");
            app.Enable = true;
            app.Update();

            // 自动注册
            var rs = service.Authorize("test", "xxx", true);
            Assert.IsNotNull(rs);

            Assert.IsNotNull(app);
            Assert.AreEqual(app.ID, rs.ID);

            // 再次验证
            var rs2 = service.Authorize("test", "xxx", false);
            Assert.IsNotNull(rs2);
            Assert.AreEqual(app.ID, rs.ID);

            // 错误验证
            Assert.ThrowsException<InvalidOperationException>(() => service.Authorize("test", "yyy", true));
        }

        [TestMethod()]
        public void IssueTokenTest()
        {
            var app = App.FindByName("test");

            var set = Setting.Current;
            var service = new AppService();
            var model = service.IssueToken(app, set);
            Assert.IsNotNull(model);
        }

        [TestMethod()]
        public void DecodeTokenTest()
        {
            Assert.Fail();
        }
    }
}