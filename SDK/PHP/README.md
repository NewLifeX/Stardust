# 星尘监控 PHP SDK

适用于 PHP 7.4+，提供星尘 APM 监控和配置中心的接入能力。

## 功能特性

- ✅ **APM 监控**：应用性能追踪、调用链分析、错误监控
- ✅ **配置中心**：集中配置管理、配置热更新、多环境支持
- ✅ **零依赖**：仅依赖 PHP 内置的 cURL 和 JSON 扩展
- ✅ **轻量级**：核心代码约 500 行，易于集成和维护
- ✅ **易于使用**：简洁的 API，几行代码即可完成接入

## 依赖要求

- PHP 7.4+
- cURL 扩展（通常默认安装）
- JSON 扩展（通常默认安装）

## 快速开始

### APM 监控

```php
require_once 'src/StardustTracer.php';

// 初始化追踪器
$tracer = new StardustTracer('http://star.example.com:6600', 'MyPHPApp', 'MySecret');
$tracer->login();

// 创建追踪片段
$span = $tracer->newSpan('业务操作');
$span->tag = '参数信息';

try {
    // 执行业务逻辑
    doSomething();
} catch (Exception $e) {
    $span->setError($e);
} finally {
    $span->finish();
}

// 请求结束时上报（自动调用，也可手动调用）
$tracer->flush();
```

### 配置中心

```php
require_once 'src/StardustConfig.php';

// 初始化配置客户端
$config = new StardustConfig('http://star.example.com:6600', 'MyPHPApp', '', 'dev');
$config->login();

// 获取所有配置
$configs = $config->getAll();

// 获取单个配置项
$dbHost = $config->get('database.host', 'localhost');
$dbPort = $config->get('database.port', 3306);

// 检查配置更新
if ($config->hasNewVersion()) {
    echo "有新版本配置等待发布\n";
}
```

## 文件说明

```
SDK/PHP/
├── src/
│   ├── StardustTracer.php      # APM 监控核心类
│   └── StardustConfig.php      # 配置中心客户端
├── examples/
│   ├── apm_basic.php           # APM 基础使用示例
│   ├── config_basic.php        # 配置中心使用示例
│   ├── laravel_middleware.php  # Laravel 中间件集成
│   └── swoole_server.php       # Swoole 常驻进程示例
└── README.md                   # 本文件
```

## 核心类说明

### StardustTracer - APM 追踪器

#### 构造函数

```php
public function __construct(
    string $server,        // 星尘服务器地址
    string $appId,         // 应用标识
    string $secret = '',   // 应用密钥
    bool $autoShutdown = true  // 是否自动注册关闭函数上报
)
```

#### 主要方法

| 方法 | 说明 |
|------|------|
| `login(): bool` | 登录获取令牌 |
| `ping(): bool` | 心跳保活，刷新令牌 |
| `newSpan(string $name, string $parentId = ''): StardustSpan` | 创建追踪片段 |
| `flush(): bool` | 上报数据到监控中心 |
| `setDebug(bool $debug): void` | 设置调试模式 |

### StardustSpan - 追踪片段

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `$id` | string | 片段ID |
| `$parentId` | string | 父片段ID |
| `$traceId` | string | 追踪ID |
| `$startTime` | int | 开始时间（毫秒） |
| `$endTime` | int | 结束时间（毫秒） |
| `$tag` | string | 标签信息 |
| `$error` | string | 错误信息 |

#### 方法

| 方法 | 说明 |
|------|------|
| `setError($error): void` | 设置错误信息 |
| `finish(): void` | 完成追踪片段 |

### StardustConfig - 配置中心客户端

#### 构造函数

```php
public function __construct(
    string $server,     // 星尘服务器地址
    string $appId,      // 应用标识
    string $secret = '',    // 应用密钥
    string $scope = ''      // 作用域（dev/test/prod）
)
```

#### 主要方法

| 方法 | 说明 |
|------|------|
| `login(): bool` | 登录获取令牌 |
| `getAll(bool $force = false): ?array` | 获取所有配置 |
| `get(string $key, $default = null)` | 获取单个配置项 |
| `getVersion(): int` | 获取当前配置版本 |
| `hasNewVersion(): bool` | 检查是否有新版本 |
| `setScope(string $scope): void` | 设置作用域 |
| `setDebug(bool $debug): void` | 设置调试模式 |

## 使用场景

### 场景1：PHP-FPM / Apache mod_php

标准的请求-响应模式，每次请求独立：

```php
// 初始化（启用自动关闭函数）
$tracer = new StardustTracer($server, $appId, $secret, true);
$tracer->login();

// 创建追踪
$span = $tracer->newSpan('处理请求');
// ... 业务逻辑
$span->finish();

// 请求结束时自动上报（通过 register_shutdown_function）
```

### 场景2：Laravel / Symfony 框架

通过中间件集成：

```php
// 参考 examples/laravel_middleware.php
class StardustMiddleware
{
    public function handle(Request $request, Closure $next)
    {
        $span = $tracer->newSpan($request->path());
        try {
            return $next($request);
        } finally {
            $span->finish();
            $tracer->flush();
        }
    }
}
```

### 场景3：Swoole / Workerman 常驻进程

需要定时上报和心跳：

```php
// 初始化（禁用自动关闭函数）
$tracer = new StardustTracer($server, $appId, $secret, false);
$tracer->login();

// 定时上报（每60秒）
Swoole\Timer::tick(60000, function () use ($tracer) {
    $tracer->flush();
});

// 定时心跳（每30秒）
Swoole\Timer::tick(30000, function () use ($tracer) {
    $tracer->ping();
});

// 请求处理
$http->on('request', function ($request, $response) use ($tracer) {
    $span = $tracer->newSpan('处理请求');
    // ... 业务逻辑
    $span->finish();
    // 不要在每个请求结束时调用 flush，由定时器统一上报
});
```

## 配置中心使用

### 基本用法

```php
$config = new StardustConfig($server, $appId, $secret, 'prod');
$config->login();

// 首次加载
$configs = $config->getAll();

// 应用配置
$dbConfig = [
    'host' => $config->get('database.host', 'localhost'),
    'port' => $config->get('database.port', 3306),
    'name' => $config->get('database.name', 'app'),
];
```

### 配置热更新（轮询模式）

```php
// 定时检查配置更新
while (true) {
    $oldVersion = $config->getVersion();
    $configs = $config->getAll();
    $newVersion = $config->getVersion();
    
    if ($newVersion > $oldVersion) {
        echo "配置已更新，重新加载...\n";
        // 重新初始化应用配置
        reloadAppConfig($configs);
    }
    
    sleep(30); // 每30秒检查一次
}
```

## 高级特性

### 嵌套追踪

```php
// 父操作
$parentSpan = $tracer->newSpan('处理订单');
$parentSpan->tag = 'orderId=12345';

// 子操作1
$childSpan1 = $tracer->newSpan('验证库存', $parentSpan->id);
// ... 业务逻辑
$childSpan1->finish();

// 子操作2
$childSpan2 = $tracer->newSpan('扣减库存', $parentSpan->id);
// ... 业务逻辑
$childSpan2->finish();

$parentSpan->finish();
```

### 错误处理

```php
$span = $tracer->newSpan('数据库操作');

try {
    // 业务逻辑
    $result = $db->query($sql);
} catch (PDOException $e) {
    // 记录异常
    $span->setError($e);
    // 可以继续抛出或处理
    throw $e;
} finally {
    $span->finish();
}
```

### 自定义标签

```php
$span = $tracer->newSpan('API调用');

// 设置详细标签信息
$span->tag = json_encode([
    'method' => 'POST',
    'url' => 'https://api.example.com/users',
    'params' => ['name' => 'John', 'age' => 30],
]);

// ... 业务逻辑

$span->finish();
```

### 调试模式

```php
// 开启调试模式，输出详细日志到 error_log
$tracer->setDebug(true);
$config->setDebug(true);

// 所有 HTTP 请求和响应都会记录到日志
```

## 性能优化

### 1. 合理设置采样率

星尘服务器会返回采样配置，SDK 会自动应用：

- `Period`: 采样周期（秒）
- `MaxSamples`: 正常请求最大采样数
- `MaxErrors`: 错误请求最大采样数

### 2. 使用 GZIP 压缩

数据量超过 1KB 时，SDK 会自动使用 GZIP 压缩：

```php
// 自动判断：
// - 数据 <= 1KB：使用 /Trace/Report
// - 数据 > 1KB：使用 /Trace/ReportRaw（GZIP 压缩）
$tracer->flush();
```

### 3. 批量上报

在常驻进程模式下，使用定时器批量上报，而不是每个请求都上报：

```php
// ✅ 推荐：定时批量上报
Swoole\Timer::tick(60000, fn() => $tracer->flush());

// ❌ 不推荐：每个请求都上报
$http->on('request', function ($req, $res) use ($tracer) {
    $span = $tracer->newSpan('request');
    $span->finish();
    $tracer->flush(); // 性能开销大
});
```

## 故障排查

### 1. 登录失败

```php
// 检查服务器地址和应用信息
$tracer = new StardustTracer('http://star.example.com:6600', 'MyApp', 'secret');
$tracer->setDebug(true); // 开启调试

if (!$tracer->login()) {
    // 查看 error_log 中的详细错误信息
    // 常见问题：
    // - 服务器地址不正确
    // - 应用不存在
    // - 密钥错误
    // - 网络不通
}
```

### 2. 数据未上报

```php
// 确保调用了 flush()
$span->finish();
$tracer->flush(); // 必须调用

// 或者依赖自动关闭函数（仅适用于 PHP-FPM 模式）
$tracer = new StardustTracer($server, $appId, $secret, true);
```

### 3. 令牌过期

```php
// SDK 会自动刷新令牌，提前 5 分钟刷新
// 如果遇到问题，可以手动调用：
$tracer->login(); // 重新登录
$tracer->ping();  // 刷新令牌
```

## 示例代码

完整示例请查看 `examples/` 目录：

- `apm_basic.php` - APM 基础使用
- `config_basic.php` - 配置中心使用
- `laravel_middleware.php` - Laravel 中间件集成
- `swoole_server.php` - Swoole 常驻进程

运行示例：

```bash
# APM 基础示例
php examples/apm_basic.php

# 配置中心示例
php examples/config_basic.php

# Swoole 服务器示例（需要安装 Swoole 扩展）
php examples/swoole_server.php
```

## 注意事项

1. **令牌管理**：SDK 会自动管理令牌的获取和刷新，提前 5 分钟自动刷新
2. **错误处理**：所有网络请求都有超时和错误处理，不会阻塞业务逻辑
3. **内存占用**：追踪数据在内存中缓存，定期上报后释放
4. **并发安全**：在 PHP-FPM 模式下每个请求独立，无并发问题；Swoole 模式需注意共享状态

## 相关链接

- 星尘项目主页：https://github.com/NewLifeX/Stardust
- 在线演示：http://star.newlifex.com
- 完整文档：https://newlifex.com

## 许可证

MIT License

## 支持

如有问题或建议，请提交 Issue：https://github.com/NewLifeX/Stardust/issues
