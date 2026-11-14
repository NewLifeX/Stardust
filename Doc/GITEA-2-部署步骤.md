# GITEA-2 部署步骤（分阶段落实）

> 版本：v1.0 | 日期：2026-08-15 | 关联：[GITEA-1-方案规划](GITEA-1-方案规划.md)
> 用法：按 G0→G5 顺序落实。带 ⚠️ 的步骤依赖 [GITEA-1](GITEA-1-方案规划.md) §6 待确认项结果，确认后再执行。命令默认在 Git Bash 中运行；数据卷统一在 `D:\ci\`（仓库外）。

---

## G0 准备 Docker 运行时（前置，阻塞项）

> ⚠️ 依赖 Q1（Rancher Desktop 切 dockerd / 装 Docker Desktop）。当前 daemon 未运行，必须先行解决，否则后续全部卡住。

1. 启用 Docker 运行时并确认可用：
   ```bash
   docker version          # 应能看到 Server 版本，而非 npipe 报错
   docker compose version  # 已有 v5.3.1
   ```
2. 建数据根目录（仓库外，不进 git）：
   ```bash
   mkdir -p /d/ci/gitea/data /d/ci/registry/data /d/ci/runner
   ```

---

## G0.5 获取并导入核心镜像（tar 路线）

> ⚠️ **镜像获取背景（2026-08-15 实测）**：本机直连 Docker Hub / MCR / ghcr 的镜像 blob 均被企业网拦截（DNS 把 `docker.io` 劫持到 Facebook 段 `31.13.94.49`，仅根路径通、blob CDN 全拦）；内网 `harbor.luxvisions-inno.com` 解析到 `10.188.23.13` 但服务不可达；本地 `127.0.0.1:10808` 代理实测不出网（实为 Rancher 内部 `rdx-proxy`）。已验证：阿里云 ACR / 华为云 SWR 的 docker 协议层可达（镜像 blob 在自家 `*.aliyuncs.com` / `*.myhuaweicloud.com` 域名，被白名单放行，但需账号登录）。**tar 路线无需任何云账号，最稳**，故采用。
> 已实测：本机 `docker load` 本地管线可用（不依赖网络）；本机已装 .NET SDK 10.0.302 / 8.0.418 / 7.0.410，故**构建阶段走宿主 dotnet，不需要 dotnet/sdk 容器镜像**。

1. 在**能联网的机器**（公司内别的机 / 家里 / 云服务器，需装 docker）生成 tar：
   ```bash
   docker pull gitea/gitea:1.23
   docker pull gitea/act_runner:latest
   docker pull registry:2
   docker save -o gitea_stack.tar gitea/gitea:1.23 gitea/act_runner:latest registry:2
   ```
   > 三个镜像合计约 500MB+，传输用 U 盘 / 内网共享 / 云盘中转均可。
2. 把 `gitea_stack.tar` 传回本机，放到 `D:\ci\images\`（新建该目录）：
   ```bash
   mkdir -p /d/ci/images
   ```
3. 本机导入（不依赖网络）：
   ```bash
   docker load -i /d/ci/images/gitea_stack.tar
   docker images | grep -E "gitea|registry"
   ```
4. 验证三镜像齐：`gitea/gitea:1.23`、`gitea/act_runner:latest`、`registry:2`。

---

## G1 部署 Gitea

1. 写 `D:\ci\docker-compose.yml`（Gitea + registry 同栈，便于管理）：
   ```yaml
   services:
     gitea:
       image: gitea/gitea:1.23
       container_name: gitea
       environment:
         - GITEA__database__DB_TYPE=sqlite3
         - GITEA__server__ROOT_URL=http://localhost:3000/
         - GITEA__actions__ENABLED=true
         - GITEA__actions__DEFAULT_ACTIONS_URL=https://gitea.com
       ports:
         - "3000:3000"
         - "222:22"
       volumes:
         - /d/ci/gitea/data:/data
       restart: unless-stopped
     registry:
       image: registry:2
       container_name: registry
       environment:
         - REGISTRY_AUTH=htpasswd   # 本机验证可临时关闭，见 G2 注
         - REGISTRY_AUTH_HTPASSWD_REALM=Registry
         - REGISTRY_AUTH_HTPASSWD_PATH=/auth/htpasswd
         - REGISTRY_STORAGE_FILESYSTEM_ROOTDIRECTORY=/var/lib/registry
       ports:
         - "5000:5000"
       volumes:
         - /d/ci/registry/data:/var/lib/registry
       restart: unless-stopped
   ```
   > DB 默认 SQLite（Q2 选 Postgres 时改为 Postgres 服务）。Actions 已在 `GITEA__actions__ENABLED=true` 开启。
2. 启动：
   ```bash
   cd /d/ci && docker compose up -d gitea
   ```
3. 浏览器开 `http://localhost:3000` → 首次安装向导建管理员（如 `stardust`/`******`，记牢）。
4. 建仓库 `stardust-flows-demo`（或把现有 Stardust 方案推送上来做真实构建对象）。

---

## G2 部署镜像库 registry:2

1. 本机验证阶段关闭鉴权以便匿名 pull/push（**仅本机**，勿外网）：把 `REGISTRY_AUTH` 整段删掉或设为空，重启：
   ```bash
   cd /d/ci && docker compose up -d registry
   ```
2. 验证可用（用 `oras` 推一个测试制品；`oras` 需先安装到 PATH）：
   ```bash
   echo hello > /tmp/test.txt
   oras push localhost:5000/test/hello:1 /tmp/test.txt
   oras pull localhost:5000/test/hello:1 -o /tmp/pulled
   ```
   > `oras` 安装：从 https://github.com/oras-project/oras/releases 取 Windows 版解压到 PATH。
3. 若用 `htpasswd` 鉴权：`docker run --rm httpd:2 htpasswd -Bc /auth/htpasswd <user> <pass>` 生成文件挂入容器。

---

## G3 部署并注册 act_runner

1. 启动 runner 容器（挂载宿主 `docker.sock` 以起任务容器）：
   ```bash
   docker run -d --name act_runner --restart unless-stopped \
     -v /var/run/docker.sock:/var/run/docker.sock \
     -v /d/ci/runner:/data \
     -e GITEA_INSTANCE_URL=http://host.docker.internal:3000 \
     -e GITEA_RUNNER_REGISTRATION_TOKEN=<TOKEN> \
     gitea/act_runner:latest
   ```
   > `<TOKEN>`：Gitea 管理后台 `站点管理 → Actions → Runners → 创建 Runner` 获取；或仓库 Settings→Actions。
   > `host.docker.internal` 在 Windows Docker 默认可达；若用 dockerd(Moby) 需确保开启。
2. 看日志确认 `online`：
   ```bash
   docker logs -f act_runner
   ```
3. Gitea 后台确认 Runner 状态为 `active`。

---

## G4 流水线：构建并推制品

1. 仓库内加 `.gitea/workflows/build.yml`：
   ```yaml
   name: build-and-push-artifact
   on: [push, workflow_dispatch]
   jobs:
     build:
       runs-on: ubuntu-latest
       # 不声明 container：job 在 act_runner 宿主直接跑，调用本机已装 .NET SDK（无需 dotnet/sdk 容器镜像）
       steps:
         - uses: actions/checkout@v4
         - name: publish
           run: dotnet publish -c Release -o out
         - name: pack
           run: tar czf app.tgz -C out .
         - name: push artifact
           run: |
             oras push ${{ secrets.REGISTRY }}/<repo>/<app>:${GITHUB_SHA::8} app.tgz
   ```
   > `<repo>/<app>`、registry 地址用仓库 Secrets（`REGISTRY=localhost:5000`，runner 内访问用 `host.docker.internal:5000` 视网络定）。
2. push 触发，Gitea Actions 页面确认：restore→publish→pack→oras push 全绿。
3. 校验制品已入镜像库：`oras pull ...` 或 `curl http://localhost:5000/v2/_catalog`。

---

## G5 对接 Stardust Flows 并端到端测试

> 把"构建"从 Flows 内的 `DotNetBuilder` 移交 Gitea Runner；Flows 侧 `K8sDeployExecutor` 改/增"制品模式"仅负责部署。

1. **Flows 改造**（制品模式）：
   - 新增/调整 `K8sDeployExecutor`：`BuildAsync` 改为**触发 Gitea 流水线并等待制品产出**（或仅在已有制品时跳过构建），不再内联 `dotnet publish` + `docker build`。
   - `DeployAsync` 渲染"基础运行时镜像 + init 容器拉制品"的 Deployment（见 GITEA-1 §5.2），`AppDeployVersion.Image` 写基础镜像、`Url` 写制品 OCI 引用。
   - `DeploymentManifestBuilder` 增加"制品模式"分支（initContainer=`curlimages/curl` 拉 `AppDeploy.Url` 解压到 emptyDir）。
2. **触发方式**：由 `PipelineTrigger.RunAsync` 先调 Gitea API 触发 workflow（`Stardust.K8s.Flows` 内加 `IGiteaClient` 薄封装，或用 `System.Net.Http` 直调 `/api/v1/repos/{owner}/{repo}/actions/workflows/{name}/dispatches`）。
3. **端到端验证**（本机 k3s）：
   ```bash
   # 跑 Flows 测试/示例，触发完整链
   STARDUST_K8S_INTEGRATION=1 dotnet test Stardust.K8s.Flows.Tests/... --filter 集成
   # 独立核对 k3s 真实状态
   kubectl get deployment <app> -n <ns>
   kubectl get pods -n <ns> -l app=<app>
   # 核对镜像库制品
   oras pull localhost:5000/<repo>/<app>:<version> -o /tmp/v
   ```
4. **一致性核对**（对齐 K8S 集成测试既有做法）：部署后镜像=基础运行时镜像、Pod Ready、制品 digest 与 Gitea 推的一致。

---

## 完成判据

- [ ] Gitea 可访问、可建仓库、Actions 已启用
- [ ] registry:5000 可匿名 push/pull 制品
- [ ] act_runner 注册为 active，能在宿主跑 `.NET` 构建（本机已装 SDK，无需 dotnet/sdk 镜像）
- [ ] 推送代码触发构建，`dotnet publish` 产物以 OCI 制品入镜像库
- [ ] 触发 Flows 后 k3s 拉起应用 Pod（基础镜像 + init 拉制品），`kubectl` 与 `oras` 双向核对一致
