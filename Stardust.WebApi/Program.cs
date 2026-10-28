using NewLife.Cube.Swagger;
using NewLife.Log;
using Stardust;
using Stardust.Web;

// 先按宿主语言初始化日志语言，保证首次写出的配置文件注释即为本地语言，避免稍后改写
StarLocalization.InitLogCulture();

// 首次启动时先按星尘约定落盘日志目录，避免首轮日志落到 Core 默认的 ../WebApiLog
StarHost.InitLogPath("../LogWebApi");

XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);

// 桥接现有 Startup（ConfigureServices / Configure）
var startup = new Startup(builder.Configuration);
builder.Host.ConfigureServices((_, services) => startup.ConfigureServices(services));

var app = builder.Build();

// 文档中间件（UseCubeSwagger/UseCubeScalar）在 Startup.Configure 方法体最前面注册，
// 使 /scalar /swagger 请求到达即优先处理，不受后续业务中间件影响。
startup.Configure(app, app.Environment);

app.Run();
