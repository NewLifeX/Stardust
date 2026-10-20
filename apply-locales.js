// 把 JSON 词条写入各项目 Locales/*.yml：先按 zh-Hans 键集剔除多余键，再更新或追加译文。
// 词条格式：{ "English key": { "ja": "译文", "de": "译文" }, ... }
//
// 用法：
//   node apply-locales.js entries.json Stardust/Locales Stardust.Server/Locales ...
//   node apply-locales.js entries.json $(所有 Locales 目录)
//
// 说明：脚本只会写入词条中出现的语言；缺失语言保持英文占位，运行时回退英文。
const fs = require('fs');
const path = require('path');

const [, , entriesFile, ...dirs] = process.argv;

// 统计模式：node apply-locales.js --missing <lang> [localeDir...]
// 默认扫描仓库内全部 Locales 目录，输出该语言仍为英文占位的键
if (entriesFile === '--missing') {
  const lang = dirs.shift();
  const targets = dirs.length > 0 ? dirs : [
    'Stardust/Locales', 'Stardust.Extensions/Locales', 'Stardust.Server/Locales', 'Stardust.Web/Locales',
    'Stardust.WebApi/Locales', 'Stardust.Data/Locales', 'StarAgent/Locales', 'StarAgent.Mobile/Locales',
    'StarGateway/Locales', 'DeployAgent/Locales', 'Plugins/AgentExpansion/Locales', 'Plugins/NetworkDetect/Locales',
  ];
  const unesc0 = (s) => s.replace(/\\(u[0-9a-fA-F]{4}|.)/g, (m, v) => {
    if (v.length === 5) return String.fromCharCode(parseInt(v.slice(1), 16));
    switch (v) { case 'n': return '\n'; case 'r': return '\r'; case 't': return '\t'; case '\\': return '\\'; case '"': return '"'; default: return m; }
  });
  const parse0 = (line) => {
    if (typeof line !== 'string') return null;
    const m = line.match(/^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$/);
    return m ? { key: unesc0(m[1]), value: unesc0(m[2]) } : null;
  };
  const missing = new Set();
  for (const dir of targets) {
    const p = path.join(dir, lang + '.yml');
    if (!fs.existsSync(p)) { console.log('missing file ' + p); continue; }
    for (const line of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
      const e = parse0(line);
      if (e && e.value === e.key) missing.add(e.key);
    }
  }
  for (const k of missing) console.log(k);
  console.log(`untranslated=${missing.size}`);
  process.exit(0);
}

const entries = JSON.parse(fs.readFileSync(entriesFile, 'utf8'));
const keys = Object.keys(entries);
if (keys.length === 0) { console.log('no entries'); process.exit(0); }

const esc = (s) => s.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\t/g, '\\t').replace(/\r/g, '\\r').replace(/\n/g, '\\n')
  .replace(/[\u0000-\u001f]/g, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
const unesc = (s) => s.replace(/\\(u[0-9a-fA-F]{4}|.)/g, (m, v) => {
  if (v.length === 5) return String.fromCharCode(parseInt(v.slice(1), 16));
  switch (v) { case 'n': return '\n'; case 'r': return '\r'; case 't': return '\t'; case '\\': return '\\'; case '"': return '"'; default: return m; }
});
const parse = (line) => {
  if (typeof line !== 'string') return null;
  const m = line.match(/^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$/);
  return m ? { key: unesc(m[1]), value: unesc(m[2]) } : null;
};
const write = (p, text) => {
  const tmp = p + '.tmp' + process.pid;
  for (let i = 0; i < 12; i++) {
    try {
      fs.writeFileSync(tmp, text, 'utf8');
      fs.renameSync(tmp, p);
      return;
    }
    catch (e) {
      try { fs.unlinkSync(tmp); } catch { }
      if (i === 11) throw e;
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 200);
    }
  }
};

let updated = 0, added = 0, pruned = 0;
for (const dir of dirs) {
  if (!fs.existsSync(dir)) { console.log('missing ' + dir); continue; }
  const refPath = path.join(dir, 'zh-Hans.yml');
  const refKeys = fs.existsSync(refPath)
    ? new Set(fs.readFileSync(refPath, 'utf8').split(/\r?\n/).map(parse).filter(Boolean).map(e => e.key))
    : null;

  for (const file of fs.readdirSync(dir).filter(f => f.endsWith('.yml'))) {
    const locale = file.replace('.yml', '');
    const p = path.join(dir, file);
    let lines = fs.readFileSync(p, 'utf8').split(/\r?\n/);
    let changed = 0;

    let removed = 0;
    if (refKeys && locale !== 'zh-Hans') {
      const before = lines.length;
      lines = lines.filter((line) => { const e = parse(line); return !e || refKeys.has(e.key); });
      removed = before - lines.length;
      pruned += removed;
    }

    const index = new Map();
    lines.forEach((line, i) => { const e = parse(line); if (e && !index.has(e.key)) index.set(e.key, i); });

    const append = [];
    for (const key of keys) {
      if (refKeys && !refKeys.has(key)) continue;

      const value = entries[key][locale];
      if (value == null || value === '') continue;

      const i = index.get(key);
      if (i == null) { append.push(`"${esc(key)}": "${esc(value)}"`); continue; }

      const current = parse(lines[i]).value;
      if (current === value) continue;

      lines[i] = `"${esc(key)}": "${esc(value)}"`;
      changed++;
    }

    if (append.length === 0 && changed === 0 && removed === 0) continue;

    if (append.length > 0) {
      while (lines.length && lines[lines.length - 1].trim() === '') lines.pop();
      lines.push('# 运行时消息补充词条');
      lines.push(...append);
      added += append.length;
    }
    write(p, lines.join('\n') + '\n');
    updated += changed;
    console.log(`${p}: changed=${changed} appended=${append.length}`);
  }
}
console.log(`updated=${updated} appended=${added} pruned=${pruned}`);
