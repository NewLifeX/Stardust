# NewLife.Stardust - 星尘分布式平台

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/stardust?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/stardust?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.stardust?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.stardust?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.stardust?label=dev%20nuget&logo=nuget)

![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.stardust.extensions?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.stardust.extensions?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.stardust.extensions?label=dev%20nuget&logo=nuget)

## 星尘
应用服务犹如宇宙中的尘埃，而作为应用服务承载体的计算机节点是为星星，星星与尘埃构成整个浩瀚星空，即为星尘！  
![XCode](Doc/星尘分布式.png)  

Stardust  星尘，轻量级分布式服务框架！  
集群管理、配置中心、性能监控、远程发布、服务治理。服务自动注册和发现，负载均衡，动态伸缩，故障转移。  
演示系统：[http://star.newlifex.com](http://star.newlifex.com)

## 核心功能
1. [x] 星尘代理。StarAgent部署在每台应用服务器，作为系统服务负责守护本地应用及监控节点性能，支持Windows/Linux/IoT  
2. [x] 节点管理。中心化管理多机器上的海量StarAgent，支持机房内服务器节点、远端桌面工控机以及物联网边缘计算节点  
3. [x] 应用管理。为每个应用系统分配接入密钥，用于配置中心、应用监控、服务注册、服务发现等  
4. [x] 配置中心。各应用从配置中心读取自己的配置信息，有利于应用的多实例部署以及集中管理  
5. [x] 监控中心。各应用（服务提供者和服务消费者）向中心上报性能指标等监控数据  
6. [x] 注册中心。各微服务应用（服务提供者）向注册中心上报自身服务信息  
7. [x] 服务发现。各业务应用（服务消费者）从注册中心拉取指定服务的提供者信息，分布式调用（不经网关）  
8. [x] 远程发布。在星尘控制台上发布应用包到多台安装有星尘代理的应用服务器或物联网边缘节点，StarAgent负责拉起并守护进程  
9. [x] 日志中心。各应用（服务提供者和服务消费者）向中心上报日志数据，统一在星尘Web查看  

### 监控中心
星尘监控介绍 [https://newlifex.com/blood/stardust_monitor](https://newlifex.com/blood/stardust_monitor)  
应用监控
![XCode](Doc/应用监控.png)  
物联网平台远程控制花园水泵的调用链 [传送门](http://star.newlifex.com/trace?id=ac15452e1649035594650011be21ec)
![XCode](Doc/调用链.png)  
关系图
![XCode](Doc/关系图.png)  


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

### ServiceMesh架构
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

## 新生命项目矩阵
各项目默认支持net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                   |
| :--------------------------------------------------------------: | :---: | -------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                             |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                    |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表      |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp）                      |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | RPC通信框架，内网高吞吐，物联网设备低开销易接入                                        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证          |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd              |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板NewLife.Templates，Web、WebApi、Service   |
|                              中间件                              |       | 对接知名中间件平台                                                                     |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证              |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                          |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                        |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持IoT平台和IoTEdge               |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                 |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图、腾讯地图、天地图                                   |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | 音频编解码库，PCM/ADPCMA/G711A/G722U/WAV/AAC                                           |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                 |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心              |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证    |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                      |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus                       |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | 简易文件存储，支持分布式系统中文件集中存储。                                           |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                      |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                       |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                      |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                 |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET6主机，应用于工业、农业、交通、医疗        |
|                          FIoT物联网平台                          | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证 |
|                        UWB高精度室内定位                         | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证        |

## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的80多个开源项目已被广泛应用于各行业，Nuget累计下载量高达300余万次。  
团队开发的大数据中间件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台FIoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IoT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
