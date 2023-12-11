using DeployAgent;
using NewLife.Log;
using NewLife.Model;
using Stardust;

// 启用控制台日志，拦截所有异常
XTrace.UseConsole();

// 初始化对象容器，提供注入能力
var services = ObjectContainer.Current;
//services.AddSingleton(XTrace.Log);

// 配置星尘。自动读取配置文件 config/star.config 中的服务器地址
var star = services.AddStardust();

services.AddHostedService<DeployWorker>();

var host = services.BuildHost();

// 异步阻塞，友好退出
await host.RunAsync();
