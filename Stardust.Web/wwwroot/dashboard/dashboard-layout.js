/**
 * 星尘看板 · 布局个性化
 * -----------------------------------------------------------------------------
 * 按页面 + 用户把卡片显隐、顺序、宽度与高度保存在 localStorage。
 * 只做预设档位，不做像素级拖拽，窄屏继续使用原有响应式规则。
 * 大屏的视角设置由 pages/wallboard.js 自行处理。
 */
(function () {
  'use strict';

  var page = document.documentElement.dataset.page || 'system';
  var userId = document.documentElement.dataset.user || '0';
  var KEY = 'sd-dashboard-layout:v1:' + page + ':' + userId;
  var state = { version: 1, cards: {} };
  var panel = null;
  var dragId = null;

  function read() {
    try {
      var raw = localStorage.getItem(KEY);
      if (!raw) return { version: 1, cards: {} };

      var value = JSON.parse(raw);
      if (!value || value.version !== 1 || !value.cards) return { version: 1, cards: {} };
      return value;
    } catch (e) {
      return { version: 1, cards: {} };
    }
  }

  function save(value) {
    try {
      localStorage.setItem(KEY, JSON.stringify(value));
    } catch (e) {
      /* 隐私模式或容量不足时忽略 */
    }
  }

  function cards() {
    return Array.prototype.slice.call(document.querySelectorAll('.sd-content [data-card]'));
  }

  function defaultSize(el) {
    var size = Number(el.dataset.defaultSize || 0);
    if (size > 0) return size;

    var match = String(el.className || '').match(/\bcol-(\d+)\b/);
    return match ? Number(match[1]) : 0;
  }

  function containerOf(el) {
    return el.closest('.kpi-row') || el.closest('.sd-grid') || el.parentElement;
  }

  function defaults() {
    var map = {};

    cards().forEach(function (el) {
      var id = el.dataset.card;
      if (!id) return;

      map[id] = {
        id: id,
        title: el.dataset.cardTitle || id,
        visible: true,
        order: 0,
        size: defaultSize(el),
        height: 'normal',
        canHeight: !!el.querySelector('.chart-md, .chart-sm, .table-wrap--scroll, .event-list'),
        isKpi: el.classList.contains('sd-kpi'),
        container: containerOf(el)
      };
    });

    // 每个容器内按 DOM 顺序编号
    var counters = [];
    cards().forEach(function (el) {
      var id = el.dataset.card;
      if (!id || !map[id]) return;

      var container = map[id].container;
      var index = counters.indexOf(container);
      if (index < 0) {
        index = counters.length;
        counters.push(container);
      }
      if (counters[index] === undefined) counters[index] = 0;
      map[id].order = counters[index]++;
    });

    return map;
  }

  function merge(saved) {
    var map = defaults();
    var data = (saved && saved.cards) || {};

    Object.keys(map).forEach(function (id) {
      var item = data[id];
      if (!item) return;

      if (typeof item.visible === 'boolean') map[id].visible = item.visible;
      if (typeof item.order === 'number' && isFinite(item.order)) map[id].order = item.order;
      if (typeof item.size === 'number' && item.size > 0) map[id].size = item.size;
      if (item.height === 'tall' || item.height === 'normal') map[id].height = item.height;
    });

    return map;
  }

  function normalize(saved) {
    var map = merge(saved);
    var data = {};

    Object.keys(map).forEach(function (id) {
      data[id] = {
        visible: map[id].visible,
        order: map[id].order,
        size: map[id].size,
        height: map[id].height
      };
    });

    return { version: 1, cards: data };
  }

  function applyHeight(el, height) {
    var chart = el.querySelector('.chart-md, .chart-sm');
    if (chart && !chart.classList.contains('chart--fill')) {
      if (!chart.dataset.defaultHeight) chart.dataset.defaultHeight = chart.style.height || '';
      chart.style.height = height === 'tall' ? '320px' : chart.dataset.defaultHeight;
    }

    var table = el.querySelector('.table-wrap--scroll');
    if (table) {
      if (!table.dataset.defaultMaxHeight) table.dataset.defaultMaxHeight = table.style.maxHeight || '';
      table.style.maxHeight = height === 'tall' ? '420px' : table.dataset.defaultMaxHeight;
    }

    var events = el.querySelector('.event-list');
    if (events) {
      if (!events.dataset.defaultMaxHeight) events.dataset.defaultMaxHeight = events.style.maxHeight || '';
      events.style.maxHeight = height === 'tall' ? '420px' : events.dataset.defaultMaxHeight;
    }
  }

  function apply() {
    var map = merge(state);

    Object.keys(map).forEach(function (id) {
      var item = map[id];
      var el = item.container ? item.container.querySelector('[data-card="' + id + '"]') : document.querySelector('[data-card="' + id + '"]');
      if (!el) return;

      el.style.display = item.visible ? '' : 'none';
      el.style.order = String(item.order);

      if (!item.isKpi && item.size > 0) {
        el.dataset.size = String(item.size);
        el.style.setProperty('--sd-span', String(item.size));
      }

      el.dataset.height = item.height;
      applyHeight(el, item.height);
    });

    if (window.SDCharts && window.SDCharts.resizeAll) setTimeout(window.SDCharts.resizeAll, 30);
  }

  function sortItems(map) {
    return Object.keys(map).map(function (id) { return map[id]; }).sort(function (a, b) {
      if (a.container === b.container) return a.order - b.order;
      return a.isKpi === b.isKpi ? 0 : a.isKpi ? -1 : 1;
    });
  }

  function sizeOptions(value) {
    return [6, 8, 12, 16, 24].map(function (size) {
      return '<option value="' + size + '"' + (size === value ? ' selected' : '') + '>' + size + ' 栏</option>';
    }).join('');
  }

  function renderPanel() {
    if (!panel) return;

    var map = merge(state);
    var list = sortItems(map);
    var html = '';

    list.forEach(function (item, index) {
      var previous = list[index - 1];
      var next = list[index + 1];
      var canUp = previous && previous.container === item.container;
      var canDown = next && next.container === item.container;

      html += '<div class="sd-layout__row" data-layout-id="' + item.id + '" draggable="true">' +
        '<span class="sd-layout__drag" title="拖拽排序">⋮⋮</span>' +
        '<label class="sd-layout__name">' +
        '<input type="checkbox" data-layout-visible' + (item.visible ? ' checked' : '') + ' />' +
        '<span>' + escapeHtml(item.title) + '</span>' +
        '</label>' +
        '<div class="sd-layout__ops">' +
        '<button type="button" data-layout-up title="上移"' + (canUp ? '' : ' disabled') + '>↑</button>' +
        '<button type="button" data-layout-down title="下移"' + (canDown ? '' : ' disabled') + '>↓</button>' +
        (item.isKpi ? '' : '<select data-layout-size title="宽度">' + sizeOptions(item.size) + '</select>') +
        (!item.isKpi && item.canHeight ? '<select data-layout-height title="高度">' +
          '<option value="normal"' + (item.height === 'normal' ? ' selected' : '') + '>标准</option>' +
          '<option value="tall"' + (item.height === 'tall' ? ' selected' : '') + '>加高</option>' +
          '</select>' : '') +
        '</div>' +
        '</div>';
    });

    panel.querySelector('[data-layout-list]').innerHTML = html;
  }

  function escapeHtml(text) {
    return String(text || '').replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  function commit(next) {
    state = normalize(next);
    save(state);
    apply();
    renderPanel();
  }

  function update(id, patch) {
    var next = read();
    var current = next.cards[id] || {};
    next.cards[id] = Object.assign({}, current, patch);
    commit(next);
  }

  function move(id, delta) {
    var map = merge(read());
    var item = map[id];
    if (!item) return;

    var siblings = Object.keys(map).map(function (key) { return map[key]; })
      .filter(function (x) { return x.container === item.container; })
      .sort(function (a, b) { return a.order - b.order; });

    var index = siblings.findIndex(function (x) { return x.id === id; });
    var target = siblings[index + delta];
    if (!target) return;

    var orders = siblings.map(function (x) { return x.order; });
    var next = read();
    next.cards[id] = Object.assign({}, next.cards[id] || {}, { order: orders[index + delta] });
    next.cards[target.id] = Object.assign({}, next.cards[target.id] || {}, { order: orders[index] });
    commit(next);
  }

  function moveBefore(sourceId, targetId) {
    if (!sourceId || !targetId || sourceId === targetId) return;

    var map = merge(read());
    var source = map[sourceId];
    var target = map[targetId];
    if (!source || !target || source.container !== target.container) return;

    var siblings = Object.keys(map).map(function (key) { return map[key]; })
      .filter(function (x) { return x.container === target.container; })
      .sort(function (a, b) { return a.order - b.order; });

    var without = siblings.filter(function (x) { return x.id !== sourceId; });
    var index = without.findIndex(function (x) { return x.id === targetId; });
    without.splice(index, 0, source);

    var next = read();
    without.forEach(function (item, i) {
      next.cards[item.id] = Object.assign({}, next.cards[item.id] || {}, { order: i });
    });
    commit(next);
  }

  function buildPanel() {
    panel = document.createElement('div');
    panel.className = 'sd-layout';
    panel.setAttribute('data-layout-panel', '');
    panel.innerHTML = '<div class="sd-layout__mask" data-layout-close></div>' +
      '<aside class="sd-layout__panel" role="dialog" aria-modal="true" aria-label="看板布局">' +
      '<header class="sd-layout__head">' +
      '<div><b>布局</b><span>仅保存在当前浏览器</span></div>' +
      '<button type="button" class="icon-btn" data-layout-close title="关闭">×</button>' +
      '</header>' +
      '<div class="sd-layout__tip">勾选显示，拖拽或上下移动排序；宽度与高度按预设档位调整。</div>' +
      '<div class="sd-layout__list" data-layout-list></div>' +
      '<footer class="sd-layout__foot">' +
      '<button type="button" class="btn" data-layout-reset>恢复默认</button>' +
      '<button type="button" class="btn btn--primary" data-layout-close>完成</button>' +
      '</footer>' +
      '</aside>';

    panel.addEventListener('click', function (e) {
      if (e.target.closest('[data-layout-close]')) {
        close();
        return;
      }
      if (e.target.closest('[data-layout-reset]')) {
        try { localStorage.removeItem(KEY); } catch (err) { }
        location.reload();
        return;
      }

      var row = e.target.closest('[data-layout-id]');
      if (!row) return;
      var id = row.dataset.layoutId;
      if (e.target.closest('[data-layout-up]')) move(id, -1);
      else if (e.target.closest('[data-layout-down]')) move(id, 1);
    });

    panel.addEventListener('change', function (e) {
      var row = e.target.closest('[data-layout-id]');
      if (!row) return;
      var id = row.dataset.layoutId;

      if (e.target.matches('[data-layout-visible]')) update(id, { visible: e.target.checked });
      else if (e.target.matches('[data-layout-size]')) update(id, { size: Number(e.target.value) || 0 });
      else if (e.target.matches('[data-layout-height]')) update(id, { height: e.target.value });
    });

    panel.addEventListener('dragstart', function (e) {
      var row = e.target.closest('[data-layout-id]');
      if (!row) return;
      dragId = row.dataset.layoutId;
      row.classList.add('is-dragging');
      if (e.dataTransfer) {
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', dragId);
      }
    });

    panel.addEventListener('dragover', function (e) {
      if (dragId && e.target.closest('[data-layout-id]')) e.preventDefault();
    });

    panel.addEventListener('drop', function (e) {
      var row = e.target.closest('[data-layout-id]');
      if (!row || !dragId) return;
      e.preventDefault();
      moveBefore(dragId, row.dataset.layoutId);
      dragId = null;
    });

    panel.addEventListener('dragend', function () {
      dragId = null;
      panel.querySelectorAll('.is-dragging').forEach(function (el) { el.classList.remove('is-dragging'); });
    });

    document.body.appendChild(panel);
  }

  function open() {
    if (!panel) buildPanel();
    renderPanel();
    panel.classList.add('is-open');
    document.documentElement.classList.add('sd-layout-open');
    var close = panel.querySelector('button[data-layout-close]');
    if (close) close.focus();
  }

  function close() {
    if (!panel) return;
    panel.classList.remove('is-open');
    document.documentElement.classList.remove('sd-layout-open');
  }

  function init() {
    state = normalize(read());
    apply();

    var btn = document.querySelector('[data-action="layout"]');
    if (btn && cards().length) btn.addEventListener('click', open);
    else if (btn) btn.style.display = 'none';

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') close();
    });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();

  window.SD = window.SD || {};
  window.SD.layout = {
    open: open,
    close: close,
    reset: function () {
      try { localStorage.removeItem(KEY); } catch (e) { }
      location.reload();
    }
  };
})();
