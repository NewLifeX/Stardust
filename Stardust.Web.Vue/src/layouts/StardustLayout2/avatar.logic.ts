// 顶栏头像文字的纯逻辑：取用户名首字符；取不到时用原型里的「你」（P-46）。
export function avatarInitial(name?: string | null, fallback = '你'): string {
  const text = (name ?? '').trim();
  if (!text) return fallback;
  // Array.from 按码点切分，避免把代理对（如生僻字）切成乱码
  return Array.from(text)[0].toUpperCase();
}
