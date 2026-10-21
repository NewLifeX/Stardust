// StarAgent 管理面板多语言。中文为源文，其余语言按 /i18n/<lang>.json 覆盖文本节点与占位提示。
// 语言以服务端 /api/languages 为准：优先固定语言，其次实际生效语言，末位跟随浏览器语言。
(function () {
  const SUPPORTED = ['zh-Hans', 'zh-Hant', 'en', 'ar', 'bn', 'cs', 'da', 'de', 'el', 'es', 'fa', 'fi', 'fr', 'he', 'hi', 'hu', 'id', 'it', 'ja', 'ko', 'ms', 'nb', 'nl', 'pl', 'pt', 'ro', 'ru', 'sv', 'th', 'tr', 'uk', 'vi'];

  function normalize(name) {
    if (!name) return null;
    let s = String(name).trim().toLowerCase();
    if (!s) return null;
    // 旧语言码归一，与 Core Localization 保持一致
    if (s === 'iw') s = 'he';
    else if (s === 'in') s = 'id';
    else if (s === 'no') s = 'nb';
    else if (s === 'mo' || s.startsWith('mo-')) s = 'ro';
    if (s.startsWith('zh')) return /hant|tw|hk|mo/.test(s) ? 'zh-Hant' : 'zh-Hans';

    const lang = s.split('-')[0];
    return SUPPORTED.find(x => x === s || x === lang) || null;
  }

  let lang = normalize(navigator.language) || 'zh-Hans';

  let dict = null;
  const sorted = [];
  const originals = new WeakMap();
  const titleSource = document.title;
  const CJK = /[\u3400-\u9fff\uf900-\ufaff]/;

  // 单字键只在两侧都不是汉字时替换，避免"核"命中"核对"之类的词
  function replaceStandalone(text, key, value) {
    let out = '', i = 0;
    while (i <= text.length) {
      const p = text.indexOf(key, i);
      if (p < 0) { out += text.slice(i); break; }

      const before = p > 0 ? text[p - 1] : '';
      const after = text[p + key.length] || '';
      if (!CJK.test(before) && !CJK.test(after)) out += text.slice(i, p) + value;
      else out += text.slice(i, p + key.length);

      i = p + key.length;
    }

    return out;
  }

  function translate(text) {
    if (!dict || !text) return text;

    const t = text.trim();
    if (!t) return text;

    const out = dict[t];
    if (out) return text.replace(t, () => out);

    if (t.length < 2 || t.length > 90) return text;

    // 含动态值的文本按最长短语替换，一轮替换完继续匹配剩余片段，覆盖"确定要启动服务吗？重启后……"这类拼句
    let rest = t;
    for (const k of sorted) {
      if (!rest.includes(k)) continue;

      const v = dict[k];
      if (v === k) continue;

      rest = k.length < 2 ? replaceStandalone(rest, k, v) : rest.split(k).join(v);
    }

    return rest === t ? text : text.replace(t, () => rest);
  }

  function translateNode(node) {
    const src = originals.get(node) || node.nodeValue;
    if (!originals.has(node)) originals.set(node, src);

    const out = translate(src);
    if (out !== node.nodeValue) node.nodeValue = out;
  }

  function translateElement(el) {
    for (const attr of ['placeholder', 'title', 'aria-label']) {
      const value = el.getAttribute && el.getAttribute(attr);
      if (!value) continue;

      const store = 'data-i18n-' + attr;
      const src = el.getAttribute(store) || value;
      if (!el.hasAttribute(store)) el.setAttribute(store, src);

      const out = translate(src);
      if (out !== value) el.setAttribute(attr, out);
    }
  }

  function translateTree(root) {
    if (!root) return;
    if (root.nodeType === 3) { translateNode(root); return; }
    if (root.nodeType !== 1 && root.nodeType !== 11) return;

    if (root.nodeType === 1) translateElement(root);

    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
    while (walker.nextNode()) {
      const node = walker.currentNode;
      if (node.nodeType === 3) translateNode(node);
      else translateElement(node);
    }
  }

  async function load(code) {
    if (code === 'zh-Hans') { dict = null; sorted.length = 0; return; }

    try {
      const res = await fetch('/i18n/' + code + '.json', { cache: 'no-cache' });
      dict = res.ok ? await res.json() : null;
    } catch { dict = null; }

    sorted.length = 0;
    if (dict) sorted.push(...Object.keys(dict).sort((a, b) => b.length - a.length));
  }

  async function setLang(code) {
    lang = normalize(code) || 'zh-Hans';

    await load(lang);

    document.documentElement.lang = lang;
    document.documentElement.dir = ['ar', 'fa', 'he'].includes(lang) ? 'rtl' : 'ltr';

    document.title = (dict && dict[titleSource.trim()]) || titleSource;

    if (dict) translateTree(document.body);
  }

  new MutationObserver(mutations => {
    if (!dict) return;

    for (const m of mutations) {
      if (m.type === 'characterData') translateNode(m.target);
      else for (const n of m.addedNodes) translateTree(n);
    }
  }).observe(document.documentElement, { childList: true, subtree: true, characterData: true });

  window.StarI18N = {
    setLang,
    supported: SUPPORTED,
    get lang() { return lang; },
    t: translate,
  };

  // 读取服务端语言清单与当前固定语言。接口不鉴权，登录页也可用
  async function loadLanguages() {
    try {
      const res = await fetch('/api/languages', { cache: 'no-store' });
      if (!res.ok) return null;

      const json = await res.json();
      return json && json.code === 0 ? json.data : null;
    } catch { return null; }
  }

  // 服务端语言名形如“德语 (Deutsch)”，优先展示括号内的本地名称
  function nativeName(name) {
    if (!name) return name;

    const m = String(name).match(/\(([^()]+)\)\s*$/);
    return m ? m[1].trim() : String(name).trim();
  }

  async function init() {
    const data = await loadLanguages();
    const current = data && data.current ? String(data.current) : '';
    const effective = data && data.effective ? String(data.effective) : '';
    const fixed = normalize(current);
    if (fixed) lang = fixed;
    else {
      // 未固定语言时跟随服务端实际生效语言，保证界面与服务端消息一致
      const actual = normalize(effective);
      if (actual) lang = actual;
    }

    const sel = document.getElementById('lang-select');
    if (sel) {
      const items = (data && data.items) || [];
      sel.innerHTML = '';
      sel.add(new Option('🌐', ''));
      if (items.length) {
        for (const it of items) sel.add(new Option(nativeName(it.name), it.code));
      } else {
        for (const code of SUPPORTED) sel.add(new Option(code, code));
      }

      if (fixed) {
        sel.value = current;
        // 服务端语言码可能不是下拉项的值，按归一化语言再匹配一次
        if (sel.value !== current) {
          const hit = items.find(e => normalize(e.code) === fixed);
          if (hit) sel.value = hit.code;
        }
      } else {
        sel.value = '';
      }

      sel.addEventListener('change', e => {
        if (window.setPanelLanguage) window.setPanelLanguage(e.target.value);
        else setLang(e.target.value);
      });
    }

    await setLang(lang);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
