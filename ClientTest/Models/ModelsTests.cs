using Stardust.Managers;
using Stardust.Models;
using Stardust.Monitors;
using Stardust.Storages;
using Xunit;

namespace ClientTest.Models;

public class VerInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void ToStringWithoutSp()
    {
        var info = new VerInfo { Name = ".NET", Version = "8.0.0" };
        Assert.Equal(".NET 8.0.0", info.ToString());
    }

    [Fact]
    [Trait("Category", "Models")]
    public void ToStringWithSp()
    {
        var info = new VerInfo { Name = ".NET Framework", Version = "4.5", Sp = "2" };
        Assert.Equal(".NET Framework 4.5 Sp2", info.ToString());
    }

    [Fact]
    [Trait("Category", "Models")]
    public void ToStringWithEmptySp()
    {
        var info = new VerInfo { Name = "CLR", Version = "4.0.30319", Sp = "" };
        Assert.Equal("CLR 4.0.30319", info.ToString());
    }
}

public class ProcessInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void DefaultProperties()
    {
        var info = new ProcessInfo
        {
            Name = "TestApp",
            ProcessId = 1234,
            ProcessName = "testapp",
            CreateTime = new DateTime(2024, 1, 1),
            UpdateTime = new DateTime(2024, 6, 1),
        };

        Assert.Equal("TestApp", info.Name);
        Assert.Equal(1234, info.ProcessId);
        Assert.Equal("testapp", info.ProcessName);
        Assert.Equal(new DateTime(2024, 1, 1), info.CreateTime);
        Assert.Equal(new DateTime(2024, 6, 1), info.UpdateTime);
    }
}

public class ServiceInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void ToString_ShowsNameAndFileName()
    {
        var info = new ServiceInfo { Name = "MyApp", FileName = "myapp.exe" };
        Assert.Equal("MyApp myapp.exe", info.ToString());
    }

    [Fact]
    [Trait("Category", "Models")]
    public void CloneProducesIndependentCopy()
    {
        var info = new ServiceInfo
        {
            Name = "MyApp",
            FileName = "myapp.exe",
            Arguments = "--port 8080",
            WorkingDirectory = "/app",
            Enable = true,
            MaxMemory = 512,
            Priority = ProcessPriority.High,
            HealthCheck = "http://localhost:8080/health",
        };

        var clone = info.Clone();

        Assert.Equal(info.Name, clone.Name);
        Assert.Equal(info.FileName, clone.FileName);
        Assert.Equal(info.Arguments, clone.Arguments);
        Assert.Equal(info.WorkingDirectory, clone.WorkingDirectory);
        Assert.Equal(info.Enable, clone.Enable);
        Assert.Equal(info.MaxMemory, clone.MaxMemory);
        Assert.Equal(info.Priority, clone.Priority);
        Assert.Equal(info.HealthCheck, clone.HealthCheck);

        clone.Name = "Changed";
        Assert.Equal("MyApp", info.Name);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void DefaultValues()
    {
        var info = new ServiceInfo { Name = "x", FileName = "x.exe" };

        Assert.False(info.Enable);
        Assert.Equal(DeployMode.Default, info.Mode);
        Assert.False(info.AllowMultiple);
        Assert.False(info.AutoStop);
        Assert.False(info.ReloadOnChange);
        Assert.Equal(0, info.MaxMemory);
        Assert.Equal(ProcessPriority.Normal, info.Priority);
        Assert.Null(info.HealthCheck);
    }
}

public class PublishServiceInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void Properties()
    {
        var info = new PublishServiceInfo
        {
            ServiceName = "TestService",
            ClientId = "127.0.0.1@1234",
            IP = "127.0.0.1",
            Version = "1.0.0",
            Address = "http://127.0.0.1:8080",
            ExternalAddress = "https://myapp.example.com",
            Health = "http://127.0.0.1:8080/health",
            Tag = "tagA,tagB",
        };

        Assert.Equal("TestService", info.ServiceName);
        Assert.Equal("127.0.0.1@1234", info.ClientId);
        Assert.Equal("127.0.0.1", info.IP);
        Assert.Equal("1.0.0", info.Version);
        Assert.Equal("http://127.0.0.1:8080", info.Address);
        Assert.Equal("https://myapp.example.com", info.ExternalAddress);
        Assert.Equal("http://127.0.0.1:8080/health", info.Health);
        Assert.Equal("tagA,tagB", info.Tag);
    }
}

public class DeployInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void Properties()
    {
        var svc = new ServiceInfo { Name = "TestApp", FileName = "app.exe" };
        var info = new DeployInfo
        {
            Id = 42,
            Name = "TestApp",
            Version = "2.0.0",
            Url = "http://cdn.example.com/app.zip",
            Hash = "abc123",
            Overwrite = "*.json;*.xml",
            Mode = DeployModes.Standard,
            Service = svc,
        };

        Assert.Equal(42, info.Id);
        Assert.Equal("TestApp", info.Name);
        Assert.Equal("2.0.0", info.Version);
        Assert.Equal(DeployModes.Standard, info.Mode);
        Assert.Same(svc, info.Service);
    }
}

public class TraceModelTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void Properties()
    {
        var model = new TraceModel
        {
            AppId = "TestApp",
            AppName = "Test Application",
            ClientId = "127.0.0.1@1234",
            Version = "1.0.0",
        };

        Assert.Equal("TestApp", model.AppId);
        Assert.Equal("Test Application", model.AppName);
        Assert.Equal("127.0.0.1@1234", model.ClientId);
        Assert.Equal("1.0.0", model.Version);
    }
}

public class TraceResponseTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void DefaultValues()
    {
        var resp = new TraceResponse();

        Assert.Equal(60, resp.Period);
        Assert.Equal(1, resp.MaxSamples);
        Assert.Equal(10, resp.MaxErrors);
        Assert.Equal(0, resp.Timeout);
        Assert.Equal(1024, resp.MaxTagLength);
        Assert.Equal(1024, resp.RequestTagLength);
        Assert.Null(resp.EnableMeter);
        Assert.Null(resp.Excludes);
    }

    [Fact]
    [Trait("Category", "Models")]
    public void SetProperties()
    {
        var resp = new TraceResponse
        {
            Period = 30,
            MaxSamples = 5,
            MaxErrors = 20,
            Timeout = 3000,
            MaxTagLength = 512,
            RequestTagLength = 256,
            EnableMeter = true,
            Excludes = ["health", "ping"],
        };

        Assert.Equal(30, resp.Period);
        Assert.Equal(5, resp.MaxSamples);
        Assert.Equal(20, resp.MaxErrors);
        Assert.Equal(3000, resp.Timeout);
        Assert.Equal(512, resp.MaxTagLength);
        Assert.Equal(256, resp.RequestTagLength);
        Assert.True(resp.EnableMeter);
        Assert.Equal(["health", "ping"], resp.Excludes);
    }
}

public class AddressInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void Properties()
    {
        var info = new AddressInfo
        {
            NodeName = "node-01",
            InternalAddress = "http://10.0.0.1:8080",
            ExternalAddress = "https://app.example.com",
        };

        Assert.Equal("node-01", info.NodeName);
        Assert.Equal("http://10.0.0.1:8080", info.InternalAddress);
        Assert.Equal("https://app.example.com", info.ExternalAddress);
    }
}

public class NewFileInfoTests
{
    [Fact]
    [Trait("Category", "Models")]
    public void Properties()
    {
        var info = new NewFileInfo
        {
            Id = 1001,
            Name = "deploy.zip",
            Path = "/files/deploy.zip",
            Hash = "sha256:abcdef",
            Length = 1024 * 1024,
            SourceNode = "node-01",
            InternalAddress = "http://10.0.0.1:5000",
            ExternalAddress = "https://cdn.example.com",
            TraceId = "trace-xyz",
        };

        Assert.Equal(1001, info.Id);
        Assert.Equal("deploy.zip", info.Name);
        Assert.Equal("/files/deploy.zip", info.Path);
        Assert.Equal("sha256:abcdef", info.Hash);
        Assert.Equal(1024 * 1024, info.Length);
        Assert.Equal("node-01", info.SourceNode);
        Assert.Equal("trace-xyz", info.TraceId);
    }
}
