// 终端纯逻辑单测（Vitest）
import { describe, it, expect } from 'vitest';
import { BLOCKED_MESSAGE, PENDING_MESSAGE, isDangerousCommand, normalizeCommand } from './terminal.logic';

describe('isDangerousCommand · 高危命令识别', () => {
  it.each([
    ['rm -rf /', 'rm -rf /'],
    ['rm   -rf /tmp/x', 'rm 与 -rf 之间多个空格'],
    ['sudo rm -rf /var', '带 sudo 前缀'],
    ['mkfs.ext4 /dev/sdb', 'mkfs'],
    ['shutdown -h now', 'shutdown'],
    ['reboot', 'reboot'],
    ['sudo reboot', 'sudo reboot'],
  ])('应识别为高危：%s（%s）', (cmd) => {
    expect(isDangerousCommand(cmd)).toBe(true);
  });

  it.each([
    ['df -h', 'df -h 放行'],
    ['uptime', 'uptime 放行'],
    ['ls -la', 'ls 放行'],
    ['rm file.txt', '无 -rf 的 rm 放行（与原型正则一致）'],
    ['', '空串放行'],
  ])('应放行：%s（%s）', (cmd) => {
    expect(isDangerousCommand(cmd)).toBe(false);
  });
});

describe('normalizeCommand · 命令归一', () => {
  it('去首尾空白', () => {
    expect(normalizeCommand('  df -h  ')).toBe('df -h');
  });

  it('空串返回 null', () => {
    expect(normalizeCommand('')).toBeNull();
  });

  it('纯空白返回 null', () => {
    expect(normalizeCommand('   \t ')).toBeNull();
  });

  it('正常命令原样', () => {
    expect(normalizeCommand('uptime')).toBe('uptime');
  });
});

describe('文案 · 终端提示', () => {
  it('拦截文案', () => {
    expect(BLOCKED_MESSAGE.startsWith('✖')).toBe(true);
    expect(BLOCKED_MESSAGE.includes('拦截')).toBe(true);
  });

  it('不声称已写入审计（前端无审计接口）', () => {
    expect(BLOCKED_MESSAGE.includes('已写入审计日志')).toBe(false);
  });

  it('待接入提示', () => {
    expect(PENDING_MESSAGE.includes('待接入')).toBe(true);
  });
});
