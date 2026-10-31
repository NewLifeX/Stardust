(function () {
  'use strict';

  var data = window.__SD_DATA__ || {};

  function p() { return SDCharts.palette; }

  function setNodeState() {
    var online = !!(data.info && data.info.online);
    document.documentElement.dataset.nodeState = online ? 'online' : 'offline';
  }

  function renderSeries() {
    var s = data.series || {};
    var el = document.getElementById('nodeSeries');
    if (!s.categories || !s.categories.length) {
      el.innerHTML = '<div class="empty" style="padding:24px 12px;"><div class="empty__title">暂无性能数据</div><div class="empty__desc">节点上线上报后自动生成资源曲线</div></div>';
      return;
    }

    var interval = Math.max(0, Math.ceil((s.categories.length || 1) / 8) - 1);
    SDCharts.multiLine('nodeSeries', {
      categories: s.categories,
      interval: interval,
      max: 100,
      unit: '%',
      series: [
        { name: 'CPU', color: p().brand, data: s.cpu, area: true },
        { name: '内存', color: p().accent, data: s.memory },
        { name: '磁盘', color: p().alert, data: s.disk }
      ]
    });
  }

  function renderRadar() {
    var r = data.radar || {};
    if (!r.indicators || !r.indicators.length) return;

    SDCharts.radar('nodeRadar', {
      indicators: r.indicators.map(function (name) { return { name: name, max: 100 }; }),
      value: r.values,
      color: r.tone === 'bad' ? p().offline : r.tone === 'warn' ? p().alert : p().accent,
      name: '健康评分'
    });
  }

  function renderGauges() {
    var n = data.network || {};
    SDCharts.gauge('gaugeIntranet', {
      name: '内网评分',
      value: Math.round(n.intranet || 0),
      color: (n.intranet || 0) >= 80 ? p().online : p().alert
    });
    SDCharts.gauge('gaugeInternet', {
      name: '外网评分',
      value: Math.round(n.internet || 0),
      color: (n.internet || 0) >= 80 ? p().online : p().alert
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
    setNodeState();
    renderSeries();
    renderRadar();
    renderGauges();
    renderSparks();
  }

  function apply(next) {
    if (!next) return;
    data = next;
    SD.applyKpis(next.kpis);
    setNodeState();
    renderSeries();
    renderGauges();
  }

  SD.onRender(renderAll);
  renderAll();
  if (data.dataUrl) SD.poll(data.dataUrl, apply, 30000);
})();
