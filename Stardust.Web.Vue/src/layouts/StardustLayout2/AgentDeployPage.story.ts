import AgentDeployPage from './AgentDeployPage.vue';
import type { AgentReq } from './agentDeploy.model';

// 请求捕获（浏览器侧）：CT 不能 Node 侧 import story（会连带转译 .vue 炸 esbuild），
// 所以 stub 里把请求挂 window.__agentCalls，spec 用 page.evaluate 读取/清空。
function captureAgent(url: string, body: unknown): void {
  const w = window as unknown as { __agentCalls?: Array<{ url: string; body: unknown }> };
  w.__agentCalls = w.__agentCalls ?? [];
  w.__agentCalls.push({ url, body });
}

// Index 预填样例（WebApi :34-40 契约：{ serverUrl, downloadUrl }）
const INDEX = { serverUrl: 'http://10.0.0.2:6600', downloadUrl: 'http://x.newlifex.com/star/' };

// 部署结果样例（WebApi :47-88 契约：List<AgentDeployResult>{host,success,message,output}）：
// 1 成功（有输出）+ 1 失败（空输出），覆盖结果表双色与输出弹窗两态
const DEPLOY_ROWS = [
  { host: '10.0.0.12', success: true, message: '部署成功', output: 'StarAgent 安装完成\n服务已启动' },
  { host: '10.0.0.13', success: false, message: 'SSH 连接失败: Connection refused', output: '' },
];

function baseReq(): AgentReq {
  return async (opts) => {
    if (opts.url === '/Nodes/AgentDeploy/Index') return { code: 0, data: INDEX };
    return { code: 0, data: null };
  };
}

// Default：Index 预填 + 测试连接成功 + 部署返回 1 成功 1 失败（CT 在同屏驱动全流程）
function stubReqDefault(): AgentReq {
  const base = baseReq();
  return async (opts) => {
    if (opts.url === '/Nodes/AgentDeploy/TestConnection') {
      captureAgent(String(opts.url), (opts.data ?? {}) as unknown);
      return { code: 0, data: { success: true, message: '连接成功，StarServer 可达' } };
    }
    if (opts.url === '/Nodes/AgentDeploy/Deploy') {
      captureAgent(String(opts.url), opts.data);
      return { code: 0, data: DEPLOY_ROWS };
    }
    return base(opts);
  };
}

// TestFail：测试连接失败分支（data.success=false，后端 message 原文透出）
function stubReqTestFail(): AgentReq {
  const base = baseReq();
  return async (opts) => {
    if (opts.url === '/Nodes/AgentDeploy/TestConnection') {
      captureAgent(String(opts.url), (opts.data ?? {}) as unknown);
      return { code: 0, data: { success: false, message: 'SSH 连接失败: Connection refused' } };
    }
    return base(opts);
  };
}

// DeployWebNotImpl：executeOn=web 部署 → code=500 后端未实现文案原样透出（C09/C10）
function stubReqWebNotImpl(): AgentReq {
  const base = baseReq();
  return async (opts) => {
    if (opts.url === '/Nodes/AgentDeploy/Deploy') {
      captureAgent(String(opts.url), opts.data);
      return { code: 500, message: 'StarWeb 本地执行暂未实现，请选择 StarServer 执行', data: null };
    }
    return base(opts);
  };
}

export const stories = [
  // 空表单 + Index 预填：「已从服务端预填」标 + firstHost 提示——CT 交互全流程在本屏驱动
  { id: 'AgentDeployPage/Default', component: AgentDeployPage, props: { deps: { request: stubReqDefault() } } },

  // 测试连接失败分支：红条 + 后端 message 原文
  { id: 'AgentDeployPage/TestFail', component: AgentDeployPage, props: { deps: { request: stubReqTestFail() } } },

  // 部署 code=500：错误条原文透出（web 未实现等）
  { id: 'AgentDeployPage/DeployWebNotImpl', component: AgentDeployPage, props: { deps: { request: stubReqWebNotImpl() } } },
];
