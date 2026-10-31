(function () {
  'use strict';

  var data = window.__SD_DATA__ || {};
  var metric = 'cost';

  function p() { return SDCharts.palette; }

  function empty(el, title, desc) {
    if (!el) return;
    el.innerHTML = '<div class="empty" style="padding:24px 12px;"><div class="empty__title">' + title + '</div><div class="empty__desc">' + (desc || '') + '</div></div>';
  }

  function renderTrend() {
    var interval = Math.max(0, Math.ceil((data.trend.categories.length || 1) / 8) - 1);
    SDCharts.areaTrend('appTrend', {
      categories: data.trend.categories,
      interval: interval,
      primary: { name: '请求量', data: data.trend.primary, unit: 'QPS', color: p().brand },
      secondary: { name: '错误率', data: data.trend.errorRate, unit: '%', formatter: '{value}%', color: p().alert }
    });
  }

  function renderInstances() {
    var ins = data.instances || {};
    SDCharts.donut('instanceDonut', {
      centerText: (ins.online || 0) + '/' + (ins.total || 0),
      centerSub: '在线实例',
      centerSize: 18,
      radius: ['62%', '84%'],
      data: [
        { name: '在线', value: ins.online || 0, itemStyle: { color: p().online } },
        { name: '离线', value: Math.max((ins.total || 0) - (ins.online || 0), 0), itemStyle: { color: p().offline } }
      ]
    });
  }

  function renderEndpoints() {
    var el = document.getElementById('endpointChart');
    var list = (data.endpoints || []).slice();
    if (!list.length) {
      empty(el, '暂无端点数据', '开启链路追踪并上报埋点后自动统计');
      return;
    }

    list.sort(function (a, b) { return b[metric] - a[metric]; });
    var top = list.slice(0, 6);
    var format = metric === 'cost'
      ? function (v) { return SD.fmt(v) + 'ms'; }
      : metric === 'errorRate'
        ? function (v) { return SD.fixed(v, 2) + '%'; }
        : function (v) { return SD.compact(v) + ' 次'; };

    SDCharts.barH('endpointChart', {
      items: top.map(function (e) { return { name: e.name, value: e[metric] }; }),
      format: format
    });
  }

  function renderErrors() {
    var el = document.getElementById('errorDonut');
    var list = (data.errorEndpoints || []).slice();
    var legend = document.getElementById('errorLegend');
    if (!list.length) {
      empty(el, '近 24 小时无错误', '端点错误率均为 0');
      if (legend) legend.innerHTML = '';
      return;
    }

    var colors = [p().offline, p().alert, p().brand2, p().brand, p().accent, p().secondary];
    var total = data.errorTotal || list.reduce(function (sum, e) { return sum + (e.errors || 0); }, 0);
    SDCharts.donut('errorDonut', {
      centerText: String(total),
      centerSub: '错误次数',
      centerSize: 20,
      data: list.map(function (e, i) {
        return { name: e.name, value: e.errors, itemStyle: { color: colors[i % colors.length] } };
      })
    });

    if (legend) {
      legend.innerHTML = list.map(function (e, i) {
        var percent = total > 0 ? (e.errors / total * 100).toFixed(1) : '0.0';
        return '<div class="code-row">' +
          '<i style="width:8px;height:8px;border-radius:2px;background:' + colors[i % colors.length] + '"></i>' +
          '<span class="grow" title="' + e.name + '">' + e.name + '</span>' +
          '<b style="color:var(--sd-text-regular);font-weight:500">' + SD.fixed(e.errors, 0) + '</b>' +
          '<span style="width:38px;text-align:right">' + percent + '%</span>' +
          '</div>';
      }).join('');
    }
  }

  function bindMetric() {
    var group = document.getElementById('endpointMetric');
    if (!group) return;
    group.addEventListener('click', function (e) {
      var btn = e.target.closest('button[data-metric]');
      if (!btn) return;
      metric = btn.dataset.metric;
      group.querySelectorAll('button').forEach(function (b) { b.classList.toggle('is-active', b === btn); });
      renderEndpoints();
    });
  }

  function renderAll() {
    renderTrend();
    renderInstances();
    renderEndpoints();
    renderErrors();
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
