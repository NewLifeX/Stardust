using NewLife.Cube.Swagger;
using NewLife.Log;
using Stardust.Web;

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);

// 解耦 WebApi 监听独立端口，避免与 Stardust.Web(Razor 管理界面, 6680) 冲突
builder.WebHost.UseUrls("http://*:6681");

// 桥接现有 Startup（ConfigureServices / Configure）
var startup = new Startup(builder.Configuration);
builder.Host.ConfigureServices((_, services) => startup.ConfigureServices(services));

var app = builder.Build();

// 文档中间件（UseCubeSwagger/UseCubeScalar）在 Startup.Configure 方法体最前面注册，
// 使 /scalar /swagger 请求到达即优先处理，不受后续业务中间件影响。
startup.Configure(app, app.Environment);

app.Run();
