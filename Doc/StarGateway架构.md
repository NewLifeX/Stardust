
### 1. 系统整体架构图
展示 StarGateway、StarAgent 与业务应用之间的拓扑关系与职责划分。

```mermaid
flowchart TD
    Internet((互联网流量\n*.newlifex.com)) -->|HTTP/HTTPS\n80/443| SG

    subgraph Server [单台服务器 / 节点]
        direction TB
        
        SG["🛡️ StarGateway\n域名路由 | SSL终结 | WebSocket代理\n负载均衡 | 空闲计时 | 端口探测"]
        
        SA["🤖 StarAgent\n系统级服务 | 进程守护\n本地 API: 127.0.0.1"]

        subgraph Apps [业务应用池 - 按需启停]
            direction LR
            AppA[App-A :5001\n🟢 运行中]
            AppB[App-B :5002\n⚪ 已休眠]
            AppC[App-C :5003\n🟢 运行中]
        end
    end

    %% 流量转发
    SG ==>|直接转发| AppA
    SG ==>|直接转发| AppC
    
    %% 控制与守护
    SG <-->|本地 HTTP API\n通知启停 / 状态同步| SA
    
    SA -.->|守护/异常拉起| SG
    SA -.->|启停控制| AppA
    SA -.->|启停控制| AppB
    SA -.->|启停控制| AppC

    %% 样式
    classDef gateway fill:#2c3e50,stroke:#3498db,stroke-width:2px,color:#fff;
    classDef agent fill:#8e44ad,stroke:#9b59b6,stroke-width:2px,color:#fff;
    classDef appRun fill:#27ae60,stroke:#2ecc71,stroke-width:2px,color:#fff;
    classDef appSleep fill:#95a5a6,stroke:#bdc3c7,stroke-width:2px,color:#fff,stroke-dasharray:5;
    
    class SG gateway;
    class SA agent;
    class AppA,AppC appRun;
    class AppB appSleep;
```

---

### 2. 请求唤醒流程（冷启动）
展示当应用处于休眠状态时，流量到达后的完整交互时序。

```mermaid
sequenceDiagram
    actor User as 用户浏览器
    participant SG as StarGateway
    participant SA as StarAgent (本地API)
    participant App as 目标应用 (App-B)

    User->>SG: 1. 请求 https://app-b.newlifex.com
    SG->>SG: 2. 域名路由匹配 -> 目标 :5002
    SG->>SG: 3. TCP 探测 :5002 端口
    
    alt 端口已监听 (应用运行中 🟢)
        SG-->>User: 直接转发请求，返回 200 OK
    else 端口未监听 (应用已休眠 ⚪)
        SG-->>User: 4. 返回 503 Service Unavailable (或加载中提示页)
        SG->>SA: 5. POST /api/start {app: "app-b"}
        SA->>App: 6. 启动进程 (App-B)
        SA-->>SG: 7. 返回 202 Accepted (启动中)
        
        loop 每秒探测一次 (直至 StartupTimeout)
            SG->>SG: 8. TCP 探测 :5002
        end
        
        SG->>SG: 9. 端口就绪，重置空闲计时器
        Note over User,SG: 10. 用户刷新或前端自动重试
        User->>SG: 11. 重试请求
        SG->>App: 12. 转发请求
        App-->>User: 13. 返回 200 OK
    end
```

---

### 3. 空闲回收流程（自动休眠）
展示应用长时间无流量时的资源回收时序。

```mermaid
sequenceDiagram
    participant SG as StarGateway
    participant SA as StarAgent (本地API)
    participant App as 目标应用 (App-B)

    Note over SG: App-B 持续无流量访问
    SG->>SG: 1. 空闲计时器达到 15 分钟 (可配)
    
    SG->>SA: 2. POST /api/stop {app: "app-b"}
    SA->>App: 3. 发送 SIGTERM (请求优雅关闭)
    
    alt 进程在超时前正常退出
        App-->>SA: 4a. 进程退出
    else 超时未退出
        SA->>App: 4b. 发送 SIGKILL (强制杀死)
        App-->>SA: 5b. 进程退出
    end
    
    SA-->>SG: 6. 返回 200 OK (已停止)
    SG->>SG: 7. 标记 App-B 为休眠状态 ⚪
    Note over SG,App: 8. 内存释放完成 ♻️
```

---

### 4. WebSocket 代理流程（升级握手）
展示客户端通过 HTTP Upgrade 机制建立 WebSocket 长连接的完整交互。

```mermaid
sequenceDiagram
    actor Client as 客户端 (浏览器/WS客户端)
    participant SG as StarGateway
    participant Backend as 后端应用

    Client->>SG: 1. HTTP GET /ws
    Note over Client,SG: Headers: Connection: Upgrade\nUpgrade: websocket\nSec-WebSocket-Key: xxx\nSec-WebSocket-Version: 13

    SG->>SG: 2. 路由匹配 -> 目标 :5002
    SG->>SG: 3. 检查路由允许 WebSocket ✅
    SG->>SG: 4. 首次记录访问日志 + APM Span
    SG->>Backend: 5. 透传原始 Upgrade 请求
    Note over SG,Backend: 保留所有 Sec-WebSocket-* 头部

    Backend-->>SG: 6. HTTP 101 Switching Protocols
    Note over Backend,SG: Headers: Connection: upgrade\nUpgrade: websocket\nSec-WebSocket-Accept: xxx\nSec-WebSocket-Protocol: (可选)

    SG-->>Client: 7. 透传 101 响应
    Note over SG: 标记连接为 "已升级" ✅
    Note over SG: 后续帧跳过 HTTP 解析/日志/Span

    par 双向帧转发 (TCP 透传)
        Client->>SG: 8a. WebSocket 数据帧 (掩码)
        SG->>Backend: 8b. 原样转发 (去除掩码由客户端协议处理)
        Backend-->>SG: 9a. WebSocket 数据帧
        SG-->>Client: 9b. 原样转发
    end

    Note over Client,Backend: Ping / Pong / 关闭帧同样透明转发

    Client->>SG: 10. Close 帧
    SG->>Backend: 11. 透传 Close 帧
    Backend-->>SG: 12. Close 帧回应
    SG-->>Client: 13. 透传 Close 帧回应
    Note over SG: 连接关闭，释放资源
```

### 5. 设计亮点总结
1. **TCP 探测解耦**：Gateway 不依赖 Agent 回报状态，直接探测端口，最真实反映应用是否具备服务能力。
2. **503 降级与重试**：冷启动期间不阻塞 Gateway 线程，通过 503 状态码让客户端（或前端 JS）自动重试，体验平滑。
3. **优雅与强制结合**：停止应用时先发 `SIGTERM` 给 .NET 应用处理收尾工作，超时再 `SIGKILL`，保证数据不丢失。
4. **WebSocket 零开销透传**：升级握手完成后，Gateway 在 TCP 层面透明转发帧数据，不解析帧内容、不产生额外日志和 APM Span，对长连接场景零性能影响。
5. **路由级 WebSocket 开关**：每路由可独立控制是否允许 WebSocket 升级，避免非目标路由被利用为 WebSocket 隧道。
6. **仅首次日志规则**：WebSocket 升级请求以外的帧不产生访问日志和链路追踪 Span，大幅降低长连接日志量。