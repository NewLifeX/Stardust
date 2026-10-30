/**
 * 星尘看板设计稿 · ECharts 封装（暗色 / 浅色双调色板）
 * -----------------------------------------------------------------------------
 * canvas 无法读取 CSS 变量，因此这里维护与 tokens.css 同源的调色板，
 * 通过 <html data-theme="light|dark"> 选择。主题切换后页面会重载并重建图表
 * （见 assets/theme.js）；StarWeb 实现时应改为监听主题变化主动重绘。
 */
(function () {
  'use strict';

  const PALETTES = {
    dark: {
      brand: '#6673FF',
      brand2: '#8C5CF5',
      accent: '#6FE3EC',
      online: '#43E8A3',
      alert: '#FFC208',
      offline: '#FF4545',
      text: '#E6EDF3',
      regular: '#C7D0DA',
      secondary: '#8A97A6',
      grid: 'rgba(58, 68, 80, .42)',
      axisLine: '#33414E',
      border: '#3A4450',
      tooltipBg: '#26333F',
      surface: '#1F2A33',
      areaTop: 0.34,
      areaBottom: 0.02,
      barTop: 0.95,
      barBottom: 0.35,
      shadow: 'rgba(102, 115, 255, .35)',
      radarArea: ['rgba(102, 115, 255, .03)', 'rgba(102, 115, 255, .07)']
    },
    light: {
      brand: '#4F5BE8',
      brand2: '#7A46E0',
      accent: '#0E8F9B',
      online: '#0B9A66',
      alert: '#B45309',
      offline: '#D92D20',
      text: '#16202A',
      regular: '#2B3A48',
      secondary: '#5C6B7A',
      grid: 'rgba(22, 32, 42, .08)',
      axisLine: '#D7E0EA',
      border: '#D7E0EA',
      tooltipBg: '#FFFFFF',
      surface: '#FFFFFF',
      areaTop: 0.22,
      areaBottom: 0.01,
      barTop: 0.95,
      barBottom: 0.62,
      shadow: 'rgba(79, 91, 232, .16)',
      radarArea: ['rgba(79, 91, 232, .03)', 'rgba(79, 91, 232, .07)']
    }
  };

  function palette() {
    return document.documentElement.dataset.theme === 'light' ? PALETTES.light : PALETTES.dark;
  }

  function styles() {
    const p = palette();
    return {
      p: p,
      axisLabel: { color: p.secondary, fontSize: 10 },
      axisLine: { lineStyle: { color: p.axisLine } },
      splitLine: { lineStyle: { color: p.grid } },
      tooltip: {
        trigger: 'axis',
        backgroundColor: p.tooltipBg,
        borderColor: p.border,
        borderWidth: 1,
        padding: [8, 10],
        textStyle: { color: p.regular, fontSize: 11 },
        axisPointer: { lineStyle: { color: p.brand, opacity: 0.35 } }
      }
    };
  }

  const charts = [];

  function fallback(el) {
    el.innerHTML = '<div class="chart-fallback">图表库未加载<br>（离线环境请联网加载 ECharts CDN）</div>';
  }

  function init(id, option) {
    const el = typeof id === 'string' ? document.getElementById(id) : id;
    if (!el) return null;
    if (!window.echarts) {
      fallback(el);
      return null;
    }
    const previous = window.echarts.getInstanceByDom(el);
    if (previous) {
      previous.dispose();
      const idx = charts.indexOf(previous);
      if (idx >= 0) charts.splice(idx, 1);
    }
    const chart = window.echarts.init(el, null, { renderer: 'canvas' });
    chart.setOption(option);
    charts.push(chart);
    return chart;
  }

  function gradient(color, topOpacity, bottomOpacity) {
    return new window.echarts.graphic.LinearGradient(0, 0, 0, 1, [
      { offset: 0, color: color + Math.round(topOpacity * 255).toString(16).padStart(2, '0') },
      { offset: 1, color: color + Math.round(bottomOpacity * 255).toString(16).padStart(2, '0') }
    ]);
  }

  /**
   * 主趋势：请求量面积 + 次要指标折线（双轴）
   */
  function areaTrend(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    const axisIndexes = o.secondary ? 1 : 0;
    const series = [
      {
        name: o.primary.name || '请求量',
        type: 'line',
        smooth: true,
        symbol: 'none',
        yAxisIndex: 0,
        lineStyle: { width: 2.4, color: o.primary.color || p.brand },
        itemStyle: { color: o.primary.color || p.brand },
        areaStyle: { color: gradient(o.primary.color || p.brand, p.areaTop, p.areaBottom) },
        data: o.primary.data
      }
    ];
    if (o.secondary) {
      series.push({
        name: o.secondary.name,
        type: o.secondary.type || 'line',
        smooth: true,
        symbol: 'none',
        yAxisIndex: axisIndexes,
        lineStyle: { width: 1.8, color: o.secondary.color || p.alert },
        itemStyle: { color: o.secondary.color || p.alert },
        areaStyle: o.secondary.bar ? undefined : { color: gradient(o.secondary.color || p.alert, p.areaTop * 0.4, 0) },
        data: o.secondary.data
      });
    }

    const yAxis = [
      {
        type: 'value',
        name: o.primary.unit || '',
        nameTextStyle: { color: p.secondary, fontSize: 10, padding: [0, 0, 0, 4] },
        axisLabel: Object.assign({}, t.axisLabel, { formatter: o.primary.formatter }),
        splitLine: t.splitLine,
        axisLine: { show: false }
      }
    ];
    if (o.secondary) {
      yAxis.push({
        type: 'value',
        name: o.secondary.unit || '',
        nameTextStyle: { color: p.secondary, fontSize: 10, padding: [0, 0, 0, 4] },
        axisLabel: Object.assign({}, t.axisLabel, { formatter: o.secondary.formatter }),
        splitLine: { show: false },
        axisLine: { show: false }
      });
    }

    return init(id, {
      animationDuration: 700,
      grid: { left: 4, right: o.secondary ? 8 : 12, top: 26, bottom: 2, containLabel: true },
      tooltip: t.tooltip,
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: o.categories,
        axisLabel: Object.assign({}, t.axisLabel, { interval: o.interval || 3 }),
        axisLine: t.axisLine,
        axisTick: { show: false }
      },
      yAxis: yAxis,
      series: series
    });
  }

  /**
   * 横向条形榜（Top 应用 / 端点 / 进程）
   */
  function barH(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    const items = o.items || [];
    const max = Math.max.apply(null, items.map(function (i) { return i.value; }).concat([1]));
    return init(id, {
      animationDuration: 600,
      grid: { left: 4, right: 42, top: 6, bottom: 2, containLabel: true },
      tooltip: Object.assign({}, t.tooltip, {
        trigger: 'item',
        formatter: function (item) {
          return item.name + '：' + item.value + (o.unit || '');
        }
      }),
      xAxis: { type: 'value', max: max * 1.08, show: false },
      yAxis: {
        type: 'category',
        inverse: true,
        data: items.map(function (i) { return i.name; }),
        axisLabel: { color: p.regular, fontSize: 11, width: 110, overflow: 'truncate' },
        axisLine: { show: false },
        axisTick: { show: false }
      },
      series: [{
        type: 'bar',
        data: items.map(function (i) {
          return {
            value: i.value,
            itemStyle: {
              color: i.color || gradient(p.brand, p.barTop, p.barBottom),
              borderRadius: [0, 4, 4, 0]
            }
          };
        }),
        barWidth: o.barWidth || 10,
        showBackground: true,
        backgroundStyle: { color: p.grid, borderRadius: 4 },
        label: {
          show: true,
          position: 'right',
          color: p.regular,
          fontSize: 11,
          formatter: function (item) { return item.value + (o.unit || ''); }
        }
      }]
    });
  }

  /**
   * 环形占比（集群健康 / 错误分布）
   */
  function donut(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    return init(id, {
      animationDuration: 800,
      tooltip: Object.assign({}, t.tooltip, { trigger: 'item', formatter: '{b}：{c}' }),
      title: {
        text: o.centerText || '',
        subtext: o.centerSub || '',
        left: 'center',
        top: o.centerTop || '38%',
        textStyle: { color: p.text, fontSize: o.centerSize || 22, fontWeight: 700 },
        subtextStyle: { color: p.secondary, fontSize: 11 }
      },
      series: [{
        type: 'pie',
        radius: o.radius || ['64%', '84%'],
        center: ['50%', '50%'],
        avoidLabelOverlap: true,
        itemStyle: { borderColor: p.surface, borderWidth: 3 },
        label: { show: false },
        labelLine: { show: false },
        data: o.data
      }]
    });
  }

  /**
   * 多线趋势（节点资源）
   */
  function multiLine(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    return init(id, {
      animationDuration: 700,
      grid: { left: 4, right: 10, top: 22, bottom: 2, containLabel: true },
      tooltip: t.tooltip,
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: o.categories,
        axisLabel: Object.assign({}, t.axisLabel, { interval: o.interval || 3 }),
        axisLine: t.axisLine,
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        max: o.max || 100,
        axisLabel: Object.assign({}, t.axisLabel, { formatter: '{value}' + (o.unit || '%') }),
        splitLine: t.splitLine,
        axisLine: { show: false }
      },
      series: (o.series || []).map(function (s) {
        return {
          name: s.name,
          type: 'line',
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 1.9, color: s.color },
          itemStyle: { color: s.color },
          areaStyle: s.area ? { color: gradient(s.color, p.areaTop * 0.55, 0) } : undefined,
          data: s.data
        };
      })
    });
  }

  /**
   * 雷达（节点健康评分）
   */
  function radar(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    return init(id, {
      animationDuration: 800,
      tooltip: Object.assign({}, t.tooltip, { trigger: 'item' }),
      radar: {
        center: ['50%', '54%'],
        radius: '66%',
        indicator: o.indicators,
        splitNumber: 4,
        axisName: { color: p.secondary, fontSize: 10 },
        axisLine: { lineStyle: { color: p.grid } },
        splitLine: { lineStyle: { color: p.grid } },
        splitArea: { areaStyle: { color: p.radarArea } }
      },
      series: [{
        type: 'radar',
        symbolSize: 4,
        lineStyle: { width: 2, color: o.color || p.accent },
        itemStyle: { color: o.color || p.accent },
        areaStyle: { color: gradient(o.color || p.accent, 0.35, 0.05) },
        data: [{ value: o.value, name: o.name || '健康评分' }]
      }]
    });
  }

  /**
   * 关系拓扑（预设坐标，避免力导向抖动）
   */
  function graph(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    const categories = o.categories || [];
    return init(id, {
      animationDuration: 800,
      tooltip: Object.assign({}, t.tooltip, {
        trigger: 'item',
        formatter: function (item) {
          if (item.dataType === 'edge') return item.data.source + ' → ' + item.data.target;
          return item.name + '：' + (item.value || 0) + (o.unit || '');
        }
      }),
      series: [{
        type: 'graph',
        layout: 'none',
        roam: false,
        zoom: o.zoom || 1,
        center: o.center,
        symbolSize: function (value, params) {
          return (params.data && params.data.symbolSize) || 26;
        },
        label: {
          show: true,
          color: p.regular,
          fontSize: 10,
          position: 'bottom',
          distance: 4
        },
        edgeSymbol: ['none', 'none'],
        lineStyle: {
          color: p.brand,
          opacity: 0.35,
          width: 1.2,
          curveness: 0.08
        },
        emphasis: {
          focus: 'adjacency',
          lineStyle: { color: p.accent, width: 2 },
          label: { color: p.text }
        },
        categories: categories,
        data: (o.nodes || []).map(function (n) {
          const c = categories[n.category || 0];
          return Object.assign({}, n, {
            itemStyle: {
              color: n.color || (c && c.itemStyle && c.itemStyle.color) || p.brand,
              borderColor: p.text,
              borderWidth: 1,
              shadowBlur: 14,
              shadowColor: p.shadow
            }
          });
        }),
        links: (o.links || []).map(function (l) {
          return Object.assign({}, l, {
            lineStyle: { width: Math.min(3, 0.6 + (l.value || 1) / 60) }
          });
        })
      }]
    });
  }

  /**
   * 堆叠/分组柱（错误分布 / 机房资源）
   */
  function stackedBar(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    return init(id, {
      animationDuration: 700,
      grid: { left: 4, right: 8, top: 18, bottom: 2, containLabel: true },
      tooltip: t.tooltip,
      xAxis: {
        type: 'category',
        data: o.categories,
        axisLabel: t.axisLabel,
        axisLine: t.axisLine,
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        axisLabel: t.axisLabel,
        splitLine: t.splitLine,
        axisLine: { show: false }
      },
      series: (o.series || []).map(function (s, i) {
        return {
          name: s.name,
          type: 'bar',
          stack: o.stacked === false ? undefined : 'total',
          barWidth: o.barWidth || 14,
          barGap: o.stacked === false ? '30%' : undefined,
          itemStyle: {
            color: s.color || p.brand,
            borderRadius: i === (o.series.length - 1) ? [4, 4, 0, 0] : 0
          },
          data: s.data
        };
      })
    });
  }

  /**
   * 仪表盘（网络质量评分）
   */
  function gauge(id, opts) {
    const o = opts || {};
    const t = styles();
    const p = t.p;
    return init(id, {
      animationDuration: 900,
      series: [{
        type: 'gauge',
        startAngle: 210,
        endAngle: -30,
        radius: '96%',
        center: ['50%', '60%'],
        min: 0,
        max: 100,
        splitNumber: 5,
        progress: { show: true, width: 7, itemStyle: { color: o.color || p.online } },
        axisLine: { lineStyle: { width: 7, color: [[1, p.grid]] } },
        axisTick: { show: false },
        splitLine: { show: false },
        axisLabel: { show: false },
        pointer: { show: false },
        anchor: { show: false },
        title: {
          show: true,
          offsetCenter: [0, '34%'],
          color: p.secondary,
          fontSize: 10
        },
        detail: {
          valueAnimation: true,
          offsetCenter: [0, '-4%'],
          color: p.text,
          fontSize: 24,
          fontWeight: 700,
          formatter: '{value}'
        },
        data: [{ value: o.value, name: o.name || '' }]
      }]
    });
  }

  window.addEventListener('resize', function () {
    charts.forEach(function (c) {
      if (c && !c.isDisposed()) c.resize();
    });
  });

  window.SDCharts = {
    areaTrend: areaTrend,
    barH: barH,
    donut: donut,
    multiLine: multiLine,
    radar: radar,
    graph: graph,
    stackedBar: stackedBar,
    gauge: gauge,
    init: init
  };

  Object.defineProperty(window.SDCharts, 'palette', {
    get: palette,
    enumerable: true
  });
})();
