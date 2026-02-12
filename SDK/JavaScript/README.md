# Stardust JavaScript SDK

星尘监控 JavaScript/Node.js SDK，提供APM（Application Performance Monitoring）性能监控能力。

## 特性

- ✅ 支持 Node.js 14+ 和现代浏览器
- ✅ 自动追踪HTTP请求（Express、Koa、fetch、XMLHttpRequest）
- ✅ 手动埋点支持
- ✅ 自动数据聚合和定时上报
- ✅ 支持GZIP压缩传输
- ✅ 零外部依赖（Node.js使用内置模块）

## 安装

```bash
# 如果发布到npm
npm install stardust-tracer

# 或直接使用源码
cp -r SDK/JavaScript/src /your-project/stardust-tracer
```

## 快速开始

### Node.js环境

```javascript
const { StardustTracer } = require('./src');

// 创建追踪器实例
const tracer = new StardustTracer(
    'http://star.example.com:6600',  // 星尘服务器地址
    'MyApp',                          // 应用ID
    'MySecret'                        // 应用密钥
);

// 启动追踪器
tracer.start();

// 手动埋点
async function businessLogic() {
    const span = tracer.newSpan('业务操作');
    span.setTag('user=123, action=create');
    
    try {
        // 执行业务逻辑
        await doSomething();
    } catch (err) {
        span.setError(err);
        throw err;
    } finally {
        span.finish();
    }
}

// 程序退出时停止追踪器
process.on('SIGINT', () => {
    tracer.stop();
    process.exit();
});
```

### 浏览器环境

```html
<!DOCTYPE html>
<html>
<head>
    <title>Stardust Browser Demo</title>
</head>
<body>
    <h1>Stardust APM 浏览器示例</h1>
    <button onclick="makeRequest()">发起请求</button>

    <script src="src/browser-tracer.js"></script>
    <script src="src/middleware/browser.js"></script>
    <script>
        // 创建浏览器追踪器
        const tracer = new StardustBrowserTracer(
            'http://star.example.com:6600',
            'MyWebApp',
            'MySecret'
        );

        // 启动追踪器
        tracer.start();

        // 安装自动拦截器（自动追踪fetch和XMLHttpRequest）
        installBrowserInterceptors(tracer);

        // 手动埋点示例
        function makeRequest() {
            const span = tracer.newSpan('用户点击按钮');
            span.setTag('action=click, button=makeRequest');
            
            fetch('/api/data')
                .then(response => response.json())
                .then(data => {
                    console.log('Data:', data);
                    span.finish();
                })
                .catch(err => {
                    span.setError(err);
                    span.finish();
                });
        }
    </script>
</body>
</html>
```

## Express中间件

```javascript
const express = require('express');
const { StardustTracer } = require('./src');
const createExpressMiddleware = require('./src/middleware/express');

const app = express();

// 创建追踪器
const tracer = new StardustTracer(
    'http://star.example.com:6600',
    'MyExpressApp',
    'MySecret'
);
tracer.start();

// 使用中间件（自动追踪所有HTTP请求）
app.use(createExpressMiddleware(tracer));

app.get('/', (req, res) => {
    res.send('Hello World!');
});

app.listen(3000, () => {
    console.log('Server running on port 3000');
});
```

## Koa中间件

```javascript
const Koa = require('koa');
const { StardustTracer } = require('./src');
const createKoaMiddleware = require('./src/middleware/koa');

const app = new Koa();

// 创建追踪器
const tracer = new StardustTracer(
    'http://star.example.com:6600',
    'MyKoaApp',
    'MySecret'
);
tracer.start();

// 使用中间件（自动追踪所有HTTP请求）
app.use(createKoaMiddleware(tracer));

app.use(async ctx => {
    ctx.body = 'Hello World!';
});

app.listen(3000, () => {
    console.log('Server running on port 3000');
});
```

## API文档

### StardustTracer (Node.js)

#### 构造函数

```javascript
new StardustTracer(server, appId, secret)
```

- `server`: 星尘服务器地址
- `appId`: 应用ID
- `secret`: 应用密钥（可选）

#### 方法

- `start()`: 启动追踪器，自动登录并开始定时上报
- `stop()`: 停止追踪器，刷新剩余数据
- `newSpan(name, parentId)`: 创建新的Span对象
  - `name`: Span名称
  - `parentId`: 父Span ID（可选）
  - 返回: Span对象

#### 配置属性

- `period`: 上报周期（秒），默认60秒
- `maxSamples`: 每个操作的最大正常样本数，默认1
- `maxErrors`: 每个操作的最大错误样本数，默认10
- `maxTagLength`: 标签最大长度，默认1024
- `excludes`: 排除的操作名称列表

### Span

#### 方法

- `setTag(tag)`: 设置标签信息
- `setError(err)`: 设置错误信息
- `finish()`: 完成Span并上报

### StardustBrowserTracer (浏览器)

#### 构造函数

```javascript
new StardustBrowserTracer(server, appId, secret)
```

参数同StardustTracer

#### 方法

- `start()`: 启动追踪器
- `stop()`: 停止追踪器
- `newSpan(name)`: 创建新的Span对象

### 浏览器拦截器

```javascript
const { installBrowserInterceptors } = require('./src/middleware/browser');

// 自动拦截fetch和XMLHttpRequest
installBrowserInterceptors(tracer);
```

## 数据上报格式

SDK会自动聚合同名Span的统计信息，包括：

- 总调用次数
- 错误次数
- 总耗时、平均耗时、最大耗时、最小耗时
- 正常样本和错误样本

数据格式示例：

```json
{
  "AppId": "MyApp",
  "AppName": "MyApp",
  "ClientId": "192.168.1.100@12345",
  "Builders": [
    {
      "Name": "GET /api/users",
      "StartTime": 1707744000000,
      "EndTime": 1707744060000,
      "Total": 150,
      "Errors": 3,
      "Cost": 45000,
      "MaxCost": 500,
      "MinCost": 50,
      "Samples": [...],
      "ErrorSamples": [...]
    }
  ]
}
```

## 配置说明

### 环境变量

可以通过环境变量配置：

```bash
# 星尘服务器地址
STARDUST_SERVER=http://star.example.com:6600

# 应用ID
STARDUST_APP_ID=MyApp

# 应用密钥
STARDUST_SECRET=MySecret
```

### 动态配置

服务器会在响应中返回配置参数，SDK会自动应用：

- `Period`: 上报周期
- `MaxSamples`: 最大样本数
- `MaxErrors`: 最大错误样本数
- `Timeout`: 超时时间
- `MaxTagLength`: 标签最大长度
- `Excludes`: 排除列表

## 最佳实践

1. **统一命名规范**: 使用一致的Span命名，如 `HTTP Method + Path`
2. **合理使用标签**: 在Tag中包含关键参数，但避免包含敏感信息
3. **错误处理**: 始终在catch块中调用`setError()`
4. **资源清理**: 应用退出时调用`stop()`确保数据上报完整
5. **性能考虑**: SDK内部已做优化，正常使用对性能影响极小

## 故障排查

### 连接失败

检查：
1. 星尘服务器地址是否正确
2. 网络连接是否正常
3. 应用ID和密钥是否正确

### 数据未上报

检查：
1. 是否调用了`start()`
2. 是否在退出前调用了`stop()`
3. 查看控制台错误日志

### 浏览器CORS问题

需要在星尘服务器端配置CORS允许跨域请求。

## 许可证

MIT License

## 相关链接

- [星尘监控平台](http://star.newlifex.com)
- [GitHub仓库](https://github.com/NewLifeX/Stardust)
- [完整文档](https://newlifex.com)
