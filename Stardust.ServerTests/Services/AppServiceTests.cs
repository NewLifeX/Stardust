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
            var rs = service.Authorize("test", "xxx", true);
            Assert.IsNotNull(rs);

            app = App.FindByName("test");
            Assert.IsNotNull(app);
            Assert.AreEqual(app.ID, rs.ID);
        }

        [TestMethod()]
        public void IssueTokenTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void DecodeTokenTest()
        {
            Assert.Fail();
        }
    }
}