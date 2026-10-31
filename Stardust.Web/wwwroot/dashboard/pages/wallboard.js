(function () {
  'use strict';

  var data = window.__SD_DATA__ || {};
  var views = ['总览', '项目', '节点', '告警'];
  var viewNames = ['GLOBAL OVERVIEW', 'PROJECT HEALTH', 'NODE RESOURCE', 'ALARM DISTRIBUTION'];
  var current = 0;
  var paused = false;
  var timer = null;
  var PERIOD = 12000;

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
      items: rows.map(function (r) { return { name: r.name, value: r.health }; }),
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
        return { name: r.name, value: r.count, itemStyle: { color: colors[i % colors.length] } };
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
      items: rows.map(function (r) { return { name: r.name, value: r.qps }; }),
      format: function (v) { return SD.fixed(v, 1) + ' QPS'; }
    });
  }

  function renderAll() {
    renderSparks();
    renderTop();
    if (current === 0) renderTrend();
    else if (current === 1) renderProjects();
    else if (current === 2) renderNodes();
    else renderAlerts();
  }

  function show(index) {
    current = (index + views.length) % views.length;
    document.querySelectorAll('.wb-pane').forEach(function (el) {
      el.classList.toggle('is-active', Number(el.dataset.pane) === current);
    });
    document.querySelectorAll('#wbDots button').forEach(function (btn) {
      btn.classList.toggle('is-active', Number(btn.dataset.view) === current);
    });
    var title = document.getElementById('wbViewName');
    if (title) title.textContent = 'STARDUST · ' + views[current] + '视角 · ' + viewNames[current];
    if (current === 0) renderTrend();
    if (current === 1) renderProjects();
    if (current === 2) renderNodes();
    if (current === 3) renderAlerts();
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
      show(Number(btn.dataset.view));
      schedule();
    });
  }

  function bindKeys() {
    document.addEventListener('keydown', function (e) {
      if (e.code !== 'Space') return;
      e.preventDefault();
      paused = !paused;
      var hint = document.getElementById('wbHint');
      if (hint) hint.textContent = paused ? '已暂停轮播 · 按空格继续 · 点击指示条切换视角' : '每 12 秒自动轮播 · 按空格暂停 · 点击指示条切换视角';
      schedule();
    });
  }

  function apply(next) {
    if (!next) return;
    data = next;
  }

  bindDots();
  bindKeys();
  SD.onRender(renderAll);
  renderAll();
  show(0);
  schedule();
  if (data.dataUrl) SD.poll(data.dataUrl, apply, 30000);
})();
