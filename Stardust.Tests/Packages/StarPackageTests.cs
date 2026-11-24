using System;
using System.IO;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests.Packages;

/// <summary>星尘包清单测试。覆盖解析、校验与路径解析（PUB-25）</summary>
public class StarPackageTests
{
    private const String Manifest = """
    {
      "schema": 1,
      "name": "newlife/star",
      "version": "4.0.2026.1123",
      "displayName": "星尘服务端",
      "apps": [
        {
          "name": "starserver",
          "fileName": "server/StarServer.dll",
          "arguments": "--urls http://*:6600",
          "workingDirectory": "server",
          "healthCheck": "http://127.0.0.1:6600/health",
          "ports": [ 6600 ],
          "main": true
        },
        {
          "name": "starweb",
          "fileName": "web/StarWeb.dll",
          "workingDirectory": "web",
          "ports": [ 8080 ]
        }
      ]
    }
    """;

    [Fact(DisplayName = "解析 camelCase 包清单并通过校验")]
    public void Parse_CamelCase_Succeeds()
    {
        var package = StarPackage.Parse(Manifest);

        Assert.Equal("newlife/star", package.Name);
        Assert.Equal("4.0.2026.1123", package.Version);
        Assert.Equal("newlife", package.Publisher);
        Assert.Equal("star", package.ShortName);
        Assert.Equal(2, package.Apps.Length);
        Assert.Empty(package.Validate());
    }

    [Fact(DisplayName = "解析 PascalCase 包清单")]
    public void Parse_PascalCase_Succeeds()
    {
        var json = """
        {
          "Schema": 1,
          "Name": "newlife/star",
          "Version": "1.0",
          "Apps": [ { "Name": "star", "FileName": "StarAgent.exe" } ]
        }
        """;

        var package = StarPackage.Parse(json);

        Assert.Equal("newlife/star", package.Name);
        Assert.Single(package.Apps);
    }

    [Theory(DisplayName = "包名校验必须为发布者/包名")]
    [InlineData("star")]
    [InlineData("/star")]
    [InlineData("newlife/")]
    [InlineData("new life/star")]
    public void Validate_InvalidName_ReturnsError(String name)
    {
        var package = new StarPackage
        {
            Name = name,
            Version = "1.0",
            Apps = [new StarPackageApp { Name = "app", FileName = "app.exe" }],
        };

        var errors = package.Validate();

        Assert.NotEmpty(errors);
    }

    [Fact(DisplayName = "没有应用时校验失败")]
    public void Validate_NoApps_ReturnsError()
    {
        var package = new StarPackage { Name = "newlife/star", Version = "1.0", Apps = [] };

        var errors = package.Validate();

        Assert.Contains(errors, e => e.Contains("At least one application"));
    }

    [Fact(DisplayName = "重复应用名校验失败")]
    public void Validate_DuplicateAppName_ReturnsError()
    {
        var package = new StarPackage
        {
            Name = "newlife/star",
            Version = "1.0",
            Apps =
            [
                new StarPackageApp { Name = "app", FileName = "a.exe" },
                new StarPackageApp { Name = "APP", FileName = "b.exe" },
            ],
        };

        var errors = package.Validate();

        Assert.Contains(errors, e => e.Contains("Duplicate application name"));
    }

    [Theory(DisplayName = "应用路径必须为安全相对路径")]
    [InlineData("../app.exe")]
    [InlineData("/app.exe")]
    [InlineData("C:\\app.exe")]
    [InlineData("a/../../b.exe")]
    public void Validate_UnsafePath_ReturnsError(String fileName)
    {
        var package = new StarPackage
        {
            Name = "newlife/star",
            Version = "1.0",
            Apps = [new StarPackageApp { Name = "app", FileName = fileName }],
        };

        var errors = package.Validate();

        Assert.Contains(errors, e => e.Contains("relative path"));
    }

    [Fact(DisplayName = "非法端口校验失败")]
    public void Validate_InvalidPort_ReturnsError()
    {
        var package = new StarPackage
        {
            Name = "newlife/star",
            Version = "1.0",
            Apps = [new StarPackageApp { Name = "app", FileName = "app.exe", Ports = [0, 70000] }],
        };

        var errors = package.Validate();

        Assert.Equal(2, errors.Count(e => e.Contains("Invalid port")));
    }

    [Fact(DisplayName = "清单参数按原值映射，不做变量替换")]
    public void ToServices_MapsFields()
    {
        var package = StarPackage.Parse(Manifest);
        var root = Path.Combine(Path.GetTempPath(), "star-pkg");

        var services = package.ToServices(root);

        Assert.Equal(2, services.Count);

        var server = services[0];
        Assert.Equal("star-starserver", server.Name);
        Assert.Equal("--urls http://*:6600", server.Arguments);
        Assert.Equal("http://127.0.0.1:6600/health", server.HealthCheck);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "server", "StarServer.dll")), server.FileName);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "server")), server.WorkingDirectory);
        Assert.Equal("newlife/star", server.Package);
        Assert.Equal("4.0.2026.1123", server.PackageVersion);
        Assert.Equal("starserver", server.PackageApp);
        Assert.True(server.Enable);
        Assert.Equal(DeployMode.Standard, server.Mode);

        var web = services[1];
        Assert.Equal("star-starweb", web.Name);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "web", "StarWeb.dll")), web.FileName);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "web")), web.WorkingDirectory);
    }

    [Theory(DisplayName = "解析操作系统名称与数字")]
    [InlineData(null, OSKind.Any)]
    [InlineData("", OSKind.Any)]
    [InlineData("linux", OSKind.Linux)]
    [InlineData("LinuxMusl", OSKind.LinuxMusl)]
    [InlineData("alpine", OSKind.LinuxMusl)]
    [InlineData("macos", OSKind.OSX)]
    [InlineData("2", OSKind.Linux)]
    public void ResolveOS_ParsesNameAndNumber(String? value, OSKind expected)
    {
        var package = new StarPackage { OS = value };

        Assert.Equal(expected, package.ResolveOS());
    }

    [Theory(DisplayName = "解析CPU架构名称与数字")]
    [InlineData(null, CpuArch.Any)]
    [InlineData("x64", CpuArch.X64)]
    [InlineData("amd64", CpuArch.X64)]
    [InlineData("aarch64", CpuArch.Arm64)]
    [InlineData("loongarch64", CpuArch.LA64)]
    [InlineData("2", CpuArch.X64)]
    public void ResolveArch_ParsesNameAndNumber(String? value, CpuArch expected)
    {
        var package = new StarPackage { Arch = value };

        Assert.Equal(expected, package.ResolveArch());
    }

    [Fact(DisplayName = "应用名等于包短名时服务名不重复")]
    public void GetServiceName_UsesShortName()
    {
        var package = new StarPackage { Name = "redis/redis" };
        var app = new StarPackageApp { Name = "redis" };

        Assert.Equal("redis", package.GetServiceName(app));
    }

    [Fact(DisplayName = "自定义服务名优先")]
    public void GetServiceName_UsesCustomName()
    {
        var package = new StarPackage { Name = "newlife/star" };
        var app = new StarPackageApp { Name = "starserver", ServiceName = "my-starserver" };

        Assert.Equal("my-starserver", package.GetServiceName(app));
    }

    [Theory(DisplayName = "安全相对路径判断")]
    [InlineData("server/a.dll", true)]
    [InlineData("a/b/../c", false)]
    [InlineData("../a", false)]
    [InlineData("/a", false)]
    [InlineData("C:\\a", false)]
    public void IsSafeRelativePath_Works(String path, Boolean expected)
    {
        Assert.Equal(expected, StarPackage.IsSafeRelativePath(path));
    }
}
