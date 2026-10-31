(function () {
  'use strict';

  var data = window.__SD_DATA__ || {};

  function p() { return SDCharts.palette; }

  function renderSparks() {
    (data.apps || []).forEach(function (app) {
      if (!app.spark || app.spark.length < 2) return;
      var el = document.querySelector('[data-app-spark="' + app.id + '"]');
      if (el) el.innerHTML = SD.spark(app.spark, 'var(--sd-color-brand)');
    });
  }

  function renderTopology() {
    var t = data.topology || { nodes: [], links: [] };
    var el = document.getElementById('projectTopology');
    if (!el) return;
    if (!t.nodes.length) {
      el.innerHTML = '<div class="empty" style="padding:24px 12px;"><div class="empty__title">暂无拓扑数据</div><div class="empty__desc">应用与节点上线后自动生成</div></div>';
      return;
    }

    SDCharts.graph('projectTopology', {
      categories: [
        { name: '项目', itemStyle: { color: p().brand } },
        { name: '应用', itemStyle: { color: p().accent } },
        { name: '节点', itemStyle: { color: p().brand2 } }
      ],
      nodes: t.nodes.map(function (n) {
        return { key: n.key, name: n.name, value: n.value, x: n.x, y: n.y, symbolSize: n.symbolSize, category: n.category, url: n.url };
      }),
      links: t.links,
      unit: '',
      format: function (v) { return SD.fixed(v, 1); },
      onClick: true
    });
  }

  function renderAll() {
    renderSparks();
    renderTopology();
  }

  function apply(next) {
    if (!next) return;
    data = next;
    SD.applyKpis(next.kpis);
    renderAll();
  }

  SD.onRender(renderAll);
  renderAll();
  if (data.dataUrl) SD.poll(data.dataUrl, apply, 30000);
})();
