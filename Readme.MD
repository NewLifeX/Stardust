#星尘
Stardust  星尘，轻量级分布式服务框架！  
设计理念: 简单，易于维护扩展！  
配置中心、集群管理、远程自动发布、服务治理。服务自动注册和发现，负载均衡，动态伸缩，故障转移，性能监控。

## 核心功能路线图
1. [ ] 星尘代理。StarAgent部署在每台应用服务器，作为系统服务，通过配置拉起并守护业务应用进程，支持Windows/Linux/IoT  
2. [x] 集群管理。中心化管理多机器上的StarAgent，支持机房内节点以及物联网边缘计算节点  
3. [x] 应用管理。为每个应用系统分配接入密钥，用于配置中心、服务注册、服务消费等  
4. [ ] 配置中心。各应用从配置中心读取自己的配置信息，有利于应用的多实例部署以及集中管理  
5. [ ] 远程发布。在星尘控制台上发布应用包到多台安装有星尘代理的应用服务器，StarAgent负责拉起并守护进程  
6. [ ] 注册中心。各微服务应用（服务提供者）向注册中心上报自身服务信息  
7. [ ] 服务消费。各业务应用（服务消费者）从注册中心拉取指定服务的提供者信息，分布式调用（不经网关）  
8. [ ] 监控中心。各应用（服务提供者和服务消费者）向中心上报性能指标等监控数据  

### 设计概要  
提供者P向星尘S注册自己拥有的10个服务接口，消费者C也登入星尘S，得到访问令牌T，查找到某个接口刚好是P提供，于是得到了P的服务地址，然后C直连P，并使用访问令牌T表明身份  

```sequence{theme="simple"}
提供者->星尘: Key登录
note over 星尘: key/secret
提供者-->>星尘: 注册服务

消费者->星尘: Key登录
星尘-->消费者: 访问令牌Token
note left of 消费者: 向提供者验明身份

消费者->星尘: 查询服务
星尘-->消费者: 服务所在提供者列表

消费者->提供者: Token令牌登录
提供者-->消费者: 登录成功

提供者-->>星尘: 汇报状态

消费者->提供者: 请求服务
提供者-->消费者: 响应数据
```

### SericeMesh架构
Sidecar设计模式正在收到越来越多的关注和采用。作为Service Mesh的重要要素，Sidecar模式对于构建高度高度可伸缩、有弹性、安全且可便于监控的微服务架构系统至关重要。它降低了与微服务架构相关的复杂性，并提供了负载平衡、服务发现、流量管理、电路中断、遥测、故障注入等功能特性。

```sequence{theme="simple"}
消费者->本机代理: 初始化
本机代理->星尘: Key登录
星尘-->本机代理: 访问令牌Token

本机代理->星尘: 查询服务
星尘-->本机代理: 服务所在提供者列表

本机代理->提供者: Token令牌登录
提供者-->本机代理: 登录成功

本机代理-->>星尘: 汇报状态

消费者->本机代理: 请求服务
本机代理->提供者: 请求服务
提供者-->本机代理: 响应数据
本机代理-->消费者: 响应数据
```

#### 新生命开发团队  
网站：http://www.NewLifeX.com  
QQ群：1600800  

## 项目位置
http://git.newlifex.com/NewLife/Stardust

## 新生命开源项目矩阵
各项目默认支持net4.5/net4.0/netstandard2.0  

|                               项目                               | 年份  |  状态  | .NET Core | 说明                                               |
| :--------------------------------------------------------------: | :---: | :----: | :-------: | -------------------------------------------------- |
|                             基础组件                             |       |        |           | 支撑其它中间件以及产品项目                         |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 维护中 |     √     | 算法、日志、网络、RPC、序列化、缓存、多线程        |
|              [XCode](https://github.com/NewLifeX/X)              | 2005  | 维护中 |     √     | 数据中间件，MySQL、SQLite、SqlServer、Oracle       |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 维护中 |     √     | 网络库，千万级吞吐率，学习gRPC、Thrift             |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 维护中 |     √     | Web魔方，权限基础框架，集成OAuth                   |
|                              中间件                              |       |        |           | 对接各知名中间件平台                               |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | 维护中 |     √     | Redis客户端，微秒级延迟，百亿级项目验证            |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | 维护中 |     √     | 支持Apache RocketMQ和阿里云消息队列                |
|   [NewLife.Thrift](https://github.com/NewLifeX/NewLife.Thrift)   | 2019  | 维护中 |     √     | Thrift协议实现                                     |
|     [NewLife.Hive](https://github.com/NewLifeX/NewLife.Hive)     | 2019  | 维护中 |     √     | 纯托管读写Hive，Hadoop数据仓库，基于Thrift协议     |
|       [NewLife.MQ](https://github.com/NewLifeX/NewLife.MQ)       | 2016  | 维护中 |     √     | 轻量级消息队列                                     |
|             [NoDb](https://github.com/NewLifeX/NoDb)             | 2017  | 开发中 |     √     | NoSQL数据库，百万级kv读写性能，持久化              |
|    [NewLife.Cache](https://github.com/NewLifeX/NewLife.Cache)    | 2018  | 维护中 |     √     | 自定义缓存服务器                                   |
|      [NewLife.Ftp](https://github.com/NewLifeX/NewLife.Ftp)      | 2008  | 维护中 |     √     | Ftp客户端实现                                      |
|    [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql)    | 2018  | 开发中 |     √     | MySql驱动                                          |
|                             产品平台                             |       |        |           | 产品平台级，编译部署即用，个性化自定义             |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 开发中 |     √     | 蚂蚁调度系统，大数据实时计算平台                   |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 开发中 |     √     | 星尘，微服务平台，分布式平台                       |
|            [XLink](https://github.com/NewLifeX/XLink)            | 2016  | 维护中 |     √     | 物联网云平台                                       |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 维护中 |     √     | 产品级反向代理                                     |
|          [XScript](https://github.com/NewLifeX/XScript)          | 2010  | 维护中 |     ×     | C#脚本引擎                                         |
|      [NewLife.DNS](https://github.com/NewLifeX/NewLife.DNS)      | 2011  | 维护中 |     ×     | DNS代理服务器                                      |
|      [NewLife.CMX](https://github.com/NewLifeX/NewLife.CMX)      | 2013  | 维护中 |     ×     | 内容管理系统                                       |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 保密中 |   C++11   | 嵌入式操作系统，完全独立自主，ARM Cortex-M芯片架构 |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | 维护中 |     ×     | Git管理系统                                        |
|                               其它                               |       |        |           |                                                    |
|           [XCoder](https://github.com/NewLifeX/XCoder)           | 2006  | 维护中 |     ×     | 码神工具，开发者必备                               |
|        [XTemplate](https://github.com/NewLifeX/XTemplate)        | 2008  | 维护中 |     ×     | 模版引擎，T4(Text Template)语法                    |
|       [X组件 .NET2.0](https://github.com/NewLifeX/X_NET20)       | 2002  | 存档中 |  .NET2.0  | 日志、网络、RPC、序列化、缓存、Windows服务、多线程 |
|       [X组件 .NET4.0](https://github.com/NewLifeX/X_NET40)       | 2002  | 存档中 |  .NET4.0  | 日志、网络、RPC、序列化、缓存、Windows服务、多线程 |
