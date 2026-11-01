(function () {
  'use strict';

  var data = window.__SD_DATA__ || {};
  var ALL_VIEWS = [0, 1, 2, 3];
  var viewLabels = ['总览', '项目', '节点', '告警'];
  var viewNames = ['GLOBAL OVERVIEW', 'PROJECT HEALTH', 'NODE RESOURCE', 'ALARM DISTRIBUTION'];
  var SETTINGS_KEY = 'sd-wallboard-settings:v1';
  var settings = loadSettings();
  var activeViews = settings.views.length ? settings.views.slice() : ALL_VIEWS.slice();
  var current = 0;
  var paused = false;
  var timer = null;
  var PERIOD = settings.interval;

  function loadSettings() {
    try {
      var raw = localStorage.getItem(SETTINGS_KEY);
      var value = raw ? JSON.parse(raw) : null;
      var views = value && Array.isArray(value.views)
        ? value.views.map(Number).filter(function (v, i, list) { return ALL_VIEWS.indexOf(v) >= 0 && list.indexOf(v) === i; })
        : [];
      var interval = value ? Number(value.interval) : 12000;
      if ([12000, 30000, 60000].indexOf(interval) < 0) interval = 12000;

      return { views: views, interval: interval };
    } catch (e) {
      return { views: ALL_VIEWS.slice(), interval: 12000 };
    }
  }

  function saveSettings() {
    try {
      localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
    } catch (e) {
      /* 隐私模式忽略 */
    }
  }

  function p() { return SDCharts.palette; }

  function renderSparks() {
    var t = SD.bucket((data.trend && data.trend.primary) || [], 36);
    var e = SD.bucket((data.trend && data.trend.errorRate) || [], 36);
    var rate = (data.nodeRateHistory || []).slice();
    if (rate.length < 2) rate = [data.nodeOnlineRate || 0, data.nodeOnlineRate || 0];
    SD.sparkInto('[data-wb-spark="nodeRate"]', rate, 'var(--sd-color-online)');
    SD.sparkInto('[data-wb-spark="requests"]', t, 'var(--sd-color-brand)');
    SD.sparkInto('[data-wb-spark="error"]', e, 'var(--sd-color-offline)');
  }

  function renderTrend() {
    SDCharts.areaTrend('wbTrend', {
      categories: data.trend.categories,
      interval: Math.max(0, Math.ceil((data.trend.categories.length || 1) / 8) - 1),
      primary: { name: '请求量', data: data.trend.primary, unit: 'QPS', color: p().brand },
      secondary: { name: '错误率', data: data.trend.errorRate, unit: '%', formatter: '{value}%', color: p().alert }
    });
  }

  function renderProjects() {
    var rows = (data.projects || []).slice(0, 6);
    if (!rows.length) {
      document.getElementById('wbProjects').innerHTML = '<div class="empty" style="padding:20px;"><div class="empty__title">暂无项目数据</div></div>';
      return;
    }
    SDCharts.barH('wbProjects', {
      items: rows.map(function (r) { return { name: r.name, value: r.health, url: r.url }; }),
      format: function (v) { return SD.fmt(v); }
    });
  }

  function renderNodes() {
    var rows = (data.regions || []).slice(0, 6);
    if (!rows.length) {
      document.getElementById('wbNodes').innerHTML = '<div class="empty" style="padding:20px;"><div class="empty__title">暂无节点资源数据</div></div>';
      return;
    }
    SDCharts.stackedBar('wbNodes', {
      stacked: false,
      barWidth: 12,
      categories: rows.map(function (r) { return r.name + ' · ' + r.nodes; }),
      urls: rows.map(function (r) { return r.url; }),
      series: [
        { name: 'CPU', color: p().brand, data: rows.map(function (r) { return r.cpu; }) },
        { name: '内存', color: p().accent, data: rows.map(function (r) { return r.memory; }) },
        { name: '磁盘', color: p().alert, data: rows.map(function (r) { return r.disk; }) }
      ]
    });
  }

  function renderAlerts() {
    var rows = data.alarmByCategory || [];
    if (!rows.length) {
      document.getElementById('wbAlerts').innerHTML = '<div class="empty" style="padding:20px;"><div class="empty__title">近 7 天暂无告警</div><div class="empty__desc">告警触发后按类别分布展示</div></div>';
      return;
    }
    var colors = [p().offline, p().alert, p().brand2, p().brand, p().accent, p().secondary];
    SDCharts.donut('wbAlerts', {
      centerText: String(data.alarms || 0),
      centerSub: '告警记录',
      data: rows.map(function (r, i) {
        return {
          name: r.name,
          value: r.count,
          itemStyle: { color: colors[i % colors.length] },
          url: '/Monitors/AlarmRecord?Q=' + encodeURIComponent(r.name)
        };
      })
    });
  }

  function renderTop() {
    var rows = (data.topApps || []).slice(0, 5);
    if (!rows.length) {
      document.getElementById('wbTopApps').innerHTML = '<div class="empty" style="padding:20px;"><div class="empty__title">暂无调用数据</div></div>';
      return;
    }
    SDCharts.barH('wbTopApps', {
      items: rows.map(function (r) { return { name: r.name, value: r.qps, url: r.url }; }),
      format: function (v) { return SD.fixed(v, 1) + ' QPS'; }
    });
  }

  function renderPane(pane) {
    if (pane === 0) renderTrend();
    else if (pane === 1) renderProjects();
    else if (pane === 2) renderNodes();
    else renderAlerts();
  }

  function renderAll() {
    renderSparks();
    renderTop();
    renderPane(activeViews[current]);
  }

  function syncDots() {
    document.querySelectorAll('#wbDots button').forEach(function (btn) {
      var view = Number(btn.dataset.view);
      btn.hidden = activeViews.indexOf(view) < 0;
      btn.classList.toggle('is-active', view === activeViews[current]);
    });
  }

  function show(index) {
    if (!activeViews.length) activeViews = ALL_VIEWS.slice();
    current = (index + activeViews.length) % activeViews.length;
    var pane = activeViews[current];
    document.querySelectorAll('.wb-pane').forEach(function (el) {
      el.classList.toggle('is-active', Number(el.dataset.pane) === pane);
    });
    syncDots();
    var title = document.getElementById('wbViewName');
    if (title) title.textContent = 'STARDUST · ' + viewLabels[pane] + '视角 · ' + viewNames[pane];
    renderPane(pane);
  }

  function schedule() {
    clearInterval(timer);
    if (paused) return;
    timer = setInterval(function () { show(current + 1); }, PERIOD);
  }

  function bindDots() {
    var dots = document.getElementById('wbDots');
    if (!dots) return;
    dots.addEventListener('click', function (e) {
      var btn = e.target.closest('button[data-view]');
      if (!btn) return;
      var index = activeViews.indexOf(Number(btn.dataset.view));
      if (index < 0) return;
      show(index);
      schedule();
    });
  }

  function updateHint() {
    var hint = document.getElementById('wbHint');
    if (!hint) return;
    hint.textContent = paused
      ? '已暂停轮播 · 按空格继续 · 点击指示条切换视角'
      : '每 ' + Math.round(PERIOD / 1000) + ' 秒自动轮播 · 按空格暂停 · 点击指示条切换视角';
  }

  function bindKeys() {
    document.addEventListener('keydown', function (e) {
      if (e.code !== 'Space') return;
      e.preventDefault();
      paused = !paused;
      updateHint();
      schedule();
    });
  }

  function openSettings() {
    var old = document.getElementById('wbSettings');
    if (old) old.remove();

    var panel = document.createElement('div');
    panel.className = 'wb-settings is-open';
    panel.id = 'wbSettings';
    panel.innerHTML = '<div class="wb-settings__mask" data-wb-close></div>' +
      '<aside class="wb-settings__panel" role="dialog" aria-modal="true" aria-label="大屏视角设置">' +
      '<header class="wb-settings__head"><b>大屏视角</b>' +
      '<button type="button" class="icon-btn" data-wb-close title="关闭">×</button></header>' +
      '<div class="wb-settings__body">' +
      '<div class="wb-settings__label">参与轮播</div>' +
      ALL_VIEWS.map(function (v) {
        return '<label class="wb-settings__check"><input type="checkbox" data-wb-view="' + v + '"' +
          (activeViews.indexOf(v) >= 0 ? ' checked' : '') + ' /> ' + viewLabels[v] + '视角</label>';
      }).join('') +
      '<div class="wb-settings__label">轮播间隔</div>' +
      '<select class="wb-settings__select" data-wb-interval>' +
      [12000, 30000, 60000].map(function (ms) {
        return '<option value="' + ms + '"' + (PERIOD === ms ? ' selected' : '') + '>' + (ms / 1000) + ' 秒</option>';
      }).join('') +
      '</select></div>' +
      '<footer class="wb-settings__foot"><button type="button" class="btn btn--primary" data-wb-close>完成</button></footer>' +
      '</aside>';

    panel.addEventListener('click', function (e) {
      if (e.target.closest('[data-wb-close]')) panel.remove();
    });

    panel.addEventListener('change', function (e) {
      if (e.target.matches('[data-wb-view]')) {
        var view = Number(e.target.dataset.wbView);
        var index = activeViews.indexOf(view);
        if (e.target.checked && index < 0) activeViews.push(view);
        if (!e.target.checked && index >= 0) activeViews.splice(index, 1);

        // 至少保留一个视角
        if (!activeViews.length) {
          activeViews.push(view);
          e.target.checked = true;
        }

        activeViews.sort(function (a, b) { return a - b; });
        settings.views = activeViews.slice();
        saveSettings();
        show(0);
        schedule();
      } else if (e.target.matches('[data-wb-interval]')) {
        PERIOD = Number(e.target.value) || 12000;
        settings.interval = PERIOD;
        saveSettings();
        updateHint();
        schedule();
      }
    });

    document.body.appendChild(panel);
  }

  function bindSettings() {
    var btn = document.querySelector('[data-action="wallboard-settings"]');
    if (btn) btn.addEventListener('click', openSettings);
  }

  function apply(next) {
    if (!next) return;
    data = next;
  }

  bindDots();
  bindKeys();
  bindSettings();
  updateHint();
  SD.onRender(renderAll);
  renderAll();
  show(0);
  schedule();
  if (data.dataUrl) SD.poll(data.dataUrl, apply, 30000);
})();
