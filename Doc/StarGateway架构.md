
### 1. 系统整体架构图
展示 StarGateway、StarAgent 与业务应用之间的拓扑关系与职责划分。

```mermaid
flowchart TD
    Internet((互联网流量\n*.newlifex.com)) -->|HTTP/HTTPS\n80/443| SG

    subgraph Server [单台服务器 / 节点]
        direction TB
        
        SG["🛡️ StarGateway\n域名路由 | SSL终结 | 负载均衡\n空闲计时 | 端口探测"]
        
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

### 设计亮点总结
1. **TCP 探测解耦**：Gateway 不依赖 Agent 回报状态，直接探测端口，最真实反映应用是否具备服务能力。
2. **503 降级与重试**：冷启动期间不阻塞 Gateway 线程，通过 503 状态码让客户端（或前端 JS）自动重试，体验平滑。
3. **优雅与强制结合**：停止应用时先发 `SIGTERM` 给 .NET 应用处理收尾工作，超时再 `SIGKILL`，保证数据不丢失。