import { describe, expect, it } from 'vitest';
import {
  DEFAULT_INSTALL_COMMAND,
  buildInstallCommand,
  normalizeAgentData,
  probeState,
} from './agentDl.logic';

describe('normalizeAgentData', () => {
  it('六组字段归一：zip/targz 取 framework，scripts/packages/dotnet 取 description', () => {
    const data = {
      server: 'http://127.0.0.1:6681',
      agent: {
        zip: [{ name: 'staragent90.zip', framework: 'net9.0', os: 'Windows/Linux', url: 'https://x.newlifex.com/star/staragent90.zip' }],
        targz: [{ name: 'staragent90.tar.gz', framework: 'net9.0', os: 'Linux', url: 'https://x.newlifex.com/star/staragent90.tar.gz' }],
        scripts: [{ name: 'star.sh (net9.0)', description: '自动安装net9.0版', url: 'https://x.newlifex.com/star/star.sh' }],
      },
      packages: [{ name: 'star.zip', description: '星尘整体安装包', url: 'https://x.newlifex.com/star/star.zip' }],
      dotnet: [{ name: 'net9.sh', description: '.NET 9.0 运行时安装脚本', url: 'https://x.newlifex.com/dotnet/net9.sh' }],
    };
    const g = normalizeAgentData(data);
    expect(g.zip).toEqual([{ name: 'staragent90.zip', meta: 'net9.0', url: 'https://x.newlifex.com/star/staragent90.zip' }]);
    expect(g.targz[0]?.meta).toBe('net9.0');
    expect(g.scripts[0]?.meta).toBe('自动安装net9.0版');
    expect(g.packages[0]?.name).toBe('star.zip');
    expect(g.dotnet[0]?.meta).toBe('.NET 9.0 运行时安装脚本');
  });

  it('非对象/缺字段 → 全空组，name 缺省「—」，大小写兼容', () => {
    expect(normalizeAgentData(null)).toEqual({ zip: [], targz: [], scripts: [], packages: [], dotnet: [] });
    const g = normalizeAgentData({ agent: { zip: [{ Name: 'a.zip', Framework: 'net8.0', url: 'u' }] } });
    expect(g.zip[0]).toEqual({ name: 'a.zip', meta: 'net8.0', url: 'u' });
  });
});

describe('buildInstallCommand', () => {
  it('清单有 targz 首行 → 命令消费其 url（基址来自 GET /Agent?format=json）', () => {
    const g = normalizeAgentData({ agent: { targz: [{ name: 'staragent90.tar.gz', framework: 'net9.0', url: 'http://127.0.0.1:6681/star/staragent90.tar.gz' }] } });
    const cmd = buildInstallCommand(g);
    expect(cmd).toContain('wget http://127.0.0.1:6681/star/staragent90.tar.gz');
    expect(cmd).toContain('./StarAgent --server');
  });

  it('清单为空/无 url → 回退默认静态命令（AgentController.cs:91 回退语义）', () => {
    expect(buildInstallCommand(null)).toBe(DEFAULT_INSTALL_COMMAND);
    expect(buildInstallCommand({ zip: [], targz: [], scripts: [], packages: [], dotnet: [] })).toBe(DEFAULT_INSTALL_COMMAND);
    expect(DEFAULT_INSTALL_COMMAND).toContain('http://x.newlifex.com/star/');
  });
});

describe('probeState', () => {
  it('online=true → 8 行字段映射，缺字段「—」', () => {
    const res = {
      code: 0,
      data: {
        online: true,
        data: { Name: 'local', FileVersion: '3.7.2026.1101', Server: 'http://127.0.0.1:6600' },
      },
    };
    const s = probeState(res);
    expect(s.online).toBe(true);
    expect(s.rows).toHaveLength(8);
    expect(s.rows[0]).toEqual({ label: 'Name', value: 'local' });
    expect(s.rows.find((r) => r.label === 'Title')).toEqual({ label: 'Title', value: '—' });
    expect(s.rows.find((r) => r.label === 'Server')?.value).toBe('http://127.0.0.1:6600');
  });

  it('online=false 或空 info → 离线', () => {
    expect(probeState({ data: { online: false, data: null } })).toEqual({ online: false, rows: [] });
    expect(probeState(null)).toEqual({ online: false, rows: [] });
    expect(probeState({ data: { online: true, data: {} } })).toEqual({ online: false, rows: [] });
  });
});
