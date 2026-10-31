(function () {
  'use strict';

  var data = window.__SD_DATA__ || {};
  var metric = 'qps';

  function p() { return SDCharts.palette; }

  function renderTrend() {
    var interval = Math.max(0, Math.ceil((data.trend.categories.length || 1) / 8) - 1);
    SDCharts.areaTrend('systemTrend', {
      categories: data.trend.categories,
      interval: interval,
      primary: { name: '请求量', data: data.trend.primary, unit: 'QPS', color: p().brand },
      secondary: { name: '错误率', data: data.trend.errorRate, unit: '%', formatter: '{value}%', color: p().alert }
    });
  }

  function renderHealth() {
    var h = data.health || {};
    SDCharts.donut('healthDonut', {
      centerText: (h.rate || 0).toFixed(1) + '%',
      centerSub: '节点在线率',
      data: [
        { name: '在线', value: h.online || 0, itemStyle: { color: p().online } },
        { name: '告警', value: h.warn || 0, itemStyle: { color: p().alert } },
        { name: '离线', value: h.offline || 0, itemStyle: { color: p().offline } }
      ]
    });
  }

  function renderTop() {
    var items = (data.topApps || []).slice().sort(function (a, b) { return b[metric] - a[metric]; }).slice(0, 6);
    if (!items.length) {
      document.getElementById('topAppsChart').innerHTML = '<div class="empty" style="padding:24px 12px;"><div class="empty__title">暂无调用数据</div><div class="empty__desc">应用上报请求后自动生成排行</div></div>';
      return;
    }

    var conf = {
      qps: { unit: ' QPS', format: function (v) { return SD.fixed(v, 1) + ' QPS'; } },
      errorRate: { unit: '%', format: function (v) { return SD.fixed(v, 2) + '%'; } },
      cost: { unit: 'ms', format: function (v) { return SD.fmt(v) + 'ms'; } }
    }[metric];

    SDCharts.barH('topAppsChart', {
      items: items.map(function (a) { return { name: a.name, value: a[metric] }; }),
      unit: conf.unit,
      format: conf.format
    });
  }

  function renderTopology() {
    var t = data.topology || { nodes: [], links: [] };
    if (!t.nodes.length || !t.links.length) {
      document.getElementById('systemTopology').innerHTML = '<div class="empty" style="padding:24px 12px;"><div class="empty__title">暂无拓扑数据</div><div class="empty__desc">接入应用并上报注册信息后自动生成</div></div>';
      return;
    }

    SDCharts.graph('systemTopology', {
      categories: [
        { name: '项目', itemStyle: { color: p().brand } },
        { name: '应用', itemStyle: { color: p().accent } }
      ],
      nodes: t.nodes.map(function (n) {
        return { key: n.key, name: n.name, value: n.value, x: n.x, y: n.y, symbolSize: n.symbolSize, category: n.category, url: n.url };
      }),
      links: t.links,
      unit: ' QPS',
      format: function (v) { return SD.fixed(v, 1); },
      onClick: true
    });
  }

  function renderRegions() {
    var rows = data.regions || [];
    if (!rows.length) {
      document.getElementById('resourceChart').innerHTML = '<div class="empty" style="padding:24px 12px;"><div class="empty__title">暂无节点资源数据</div><div class="empty__desc">节点上线并上报性能后自动生成</div></div>';
      return;
    }

    SDCharts.stackedBar('resourceChart', {
      stacked: false,
      barWidth: 12,
      categories: rows.map(function (r) { return r.name + ' · ' + r.nodes + ' 节点'; }),
      series: [
        { name: 'CPU', color: p().brand, data: rows.map(function (r) { return r.cpu; }) },
        { name: '内存', color: p().accent, data: rows.map(function (r) { return r.memory; }) },
        { name: '磁盘', color: p().alert, data: rows.map(function (r) { return r.disk; }) }
      ]
    });
  }

  function renderSparks() {
    (data.kpis || []).forEach(function (k) {
      if (!k.spark || !k.spark.length) return;
      var el = document.querySelector('[data-kpi="' + k.key + '"] [data-kpi-spark]');
      if (el) el.innerHTML = SD.spark(k.spark, k.sparkColor || 'var(--sd-color-brand)');
    });
  }

  function renderAll() {
    renderTrend();
    renderHealth();
    renderTop();
    renderTopology();
    renderRegions();
    renderSparks();
  }

  function bindMetric() {
    var group = document.getElementById('topMetric');
    if (!group) return;
    group.addEventListener('click', function (e) {
      var btn = e.target.closest('button[data-metric]');
      if (!btn) return;
      metric = btn.dataset.metric;
      group.querySelectorAll('button').forEach(function (b) { b.classList.toggle('is-active', b === btn); });
      renderTop();
    });
  }

  function apply(next) {
    if (!next) return;
    data = next;
    SD.applyKpis(next.kpis);
    renderAll();
  }

  bindMetric();
  SD.onRender(renderAll);
  renderAll();
  if (data.dataUrl) SD.poll(data.dataUrl, apply, 30000);
})();
