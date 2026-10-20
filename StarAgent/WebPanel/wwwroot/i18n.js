// StarAgent 管理面板多语言。中文为源文，其余语言按 /i18n/<lang>.json 覆盖文本节点与占位提示。
// 语言优先级：?lang= > localStorage > 浏览器语言 > zh-Hans。
(function () {
  const SUPPORTED = ['zh-Hans', 'zh-Hant', 'en', 'ar', 'bn', 'cs', 'da', 'de', 'el', 'es', 'fa', 'fi', 'fr', 'he', 'hi', 'hu', 'id', 'it', 'ja', 'ko', 'ms', 'nb', 'nl', 'pl', 'pt', 'ro', 'ru', 'sv', 'th', 'tr', 'uk', 'vi'];

  function normalize(name) {
    if (!name) return null;
    const s = String(name).trim().toLowerCase();
    if (!s) return null;
    if (s.startsWith('zh')) return /hant|tw|hk|mo/.test(s) ? 'zh-Hant' : 'zh-Hans';

    const lang = s.split('-')[0];
    return SUPPORTED.find(x => x === s || x === lang) || null;
  }

  let lang = normalize(new URLSearchParams(location.search).get('lang'))
    || normalize(window.localStorage.getItem('staragent.lang'))
    || normalize(navigator.language)
    || 'zh-Hans';

  let dict = null;
  const sorted = [];
  const originals = new WeakMap();

  function translate(text) {
    if (!dict || !text) return text;

    const t = text.trim();
    if (!t) return text;

    let out = dict[t];
    if (!out && t.length > 1 && t.length <= 90) {
      // 含动态值的文本按最长短语替换，单字键只做整串匹配，避免误伤
      for (const k of sorted) {
        if (k.length >= 2 && t.includes(k)) { out = t.split(k).join(dict[k]); break; }
      }
    }

    return out ? text.replace(t, out) : text;
  }

  function translateNode(node) {
    const src = originals.get(node) || node.nodeValue;
    if (!originals.has(node)) originals.set(node, src);

    const out = translate(src);
    if (out !== node.nodeValue) node.nodeValue = out;
  }

  function translateElement(el) {
    for (const attr of ['placeholder', 'title']) {
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

  async function setLang(code, persist) {
    lang = normalize(code) || 'zh-Hans';
    if (persist) window.localStorage.setItem('staragent.lang', lang);

    await load(lang);

    document.documentElement.lang = lang;
    document.documentElement.dir = ['ar', 'fa', 'he'].includes(lang) ? 'rtl' : 'ltr';
    if (dict) translateTree(document.body);

    const sel = document.getElementById('lang-select');
    if (sel) sel.value = lang;
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

  async function init() {
    const sel = document.getElementById('lang-select');
    if (sel) {
      for (const code of SUPPORTED) sel.add(new Option(code, code));
      sel.addEventListener('change', e => setLang(e.target.value, true));
    }

    await setLang(lang, false);

    // 语言切换后重绘当前面板，确保服务端消息与界面语言一致
    if (dict && window.switchPanel) {
      const active = document.querySelector('#nav-bar a.active');
      if (active) switchPanel(active.dataset.panel);
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
