/**
 * 星尘看板 · 公共脚本（MVC 版）
 * -----------------------------------------------------------------------------
 * 1. 主题三态（跟随系统 / 浅色 / 深色），写入 localStorage['sd-theme']，
 *    落地在 <html data-theme data-theme-mode>；切换时不刷新页面，
 *    派发 sd:themechange 并重跑已注册的渲染函数（ECharts 需要重建）。
 * 2. ECharts 封装（暗色 / 浅色双调色板），与设计稿 assets/charts.js 同源。
 * 3. 通用工具：数字格式化、水位等级、sparkline、相对时间、时钟。
 * 4. 自动刷新：轮询 /Dashboard/Data，页面注册的 apply 函数负责更新数字与图表。
 */
(function () {
  'use strict';

  /* ============================== 主题 ============================== */

  var THEME_KEY = 'sd-theme';
  var MODES = ['auto', 'light', 'dark'];
  var LABELS = { auto: '跟随系统', light: '浅色', dark: '深色' };
  var mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  var ICONS = {
    auto:
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
      '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/></svg>',
    light:
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
      '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>',
    dark:
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
      '<path d="M21 12.8A8.5 8.5 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg>'
  };

  function storedMode() {
    try {
      var v = localStorage.getItem(THEME_KEY);
      return MODES.indexOf(v) >= 0 ? v : 'auto';
    } catch (e) {
      return 'auto';
    }
  }

  function resolveMode(mode) {
    if (mode === 'auto') return mq && mq.matches ? 'dark' : 'light';
    return mode;
  }

  function applyTheme(mode) {
    var m = mode || storedMode();
    document.documentElement.dataset.themeMode = m;
    document.documentElement.dataset.theme = resolveMode(m);
  }

  function renderThemeSwitches() {
    var current = storedMode();
    document.querySelectorAll('[data-theme-switch]').forEach(function (el) {
      el.classList.add('theme-switch');
      el.setAttribute('role', 'group');
      el.setAttribute('aria-label', '主题模式');
      el.innerHTML = MODES.map(function (m) {
        return '<button type="button" data-theme-mode="' + m + '"' +
          ' class="' + (current === m ? 'is-active' : '') + '"' +
          ' title="' + LABELS[m] + '" aria-label="' + LABELS[m] + '"' +
          ' aria-pressed="' + (current === m) + '">' + ICONS[m] + '</button>';
      }).join('');
      el.onclick = function (e) {
        var btn = e.target.closest('button[data-theme-mode]');
        if (btn) setTheme(btn.dataset.themeMode);
      };
    });
  }

  function setTheme(mode) {
    if (MODES.indexOf(mode) < 0) return;
    try {
      localStorage.setItem(THEME_KEY, mode);
    } catch (e) { /* 隐私模式下忽略 */ }
    applyTheme(mode);
    renderThemeSwitches();
    document.dispatchEvent(new CustomEvent('sd:themechange', {
      detail: { mode: mode, theme: resolveMode(mode) }
    }));
    rerender();
  }

  if (mq) {
    var onSystemThemeChange = function () {
      if (storedMode() !== 'auto') return;
      applyTheme('auto');
      rerender();
    };
    if (mq.addEventListener) mq.addEventListener('change', onSystemThemeChange);
    else if (mq.addListener) mq.addListener(onSystemThemeChange);
  }

  // 首屏内联脚本已提前设置，此处只做兜底
  applyTheme();

  /* ============================== 工具 ============================== */

  function isNumber(v) {
    return v !== null && v !== undefined && !Number.isNaN(Number(v));
  }

  function fmt(n) {
    if (!isNumber(n)) return '—';
    return Number(n).toLocaleString('en-US');
  }

  /** 自动缩写：万 / 亿 */
  function compact(n) {
    if (!isNumber(n)) return '—';
    var v = Number(n);
    if (Math.abs(v) >= 100000000) return trimZero((v / 100000000).toFixed(2)) + ' 亿';
    if (Math.abs(v) >= 10000) return trimZero((v / 10000).toFixed(1)) + ' 万';
    return fmt(Math.round(v));
  }

  function trimZero(s) {
    return String(s).replace(/\.?0+$/, '');
  }

  function fixed(n, digits) {
    if (!isNumber(n)) return '—';
    return Number(n).toFixed(digits === undefined ? 2 : digits);
  }

  function bytes(n) {
    if (!isNumber(n)) return '—';
    var v = Number(n);
    if (v >= 1099511627776) return trimZero((v / 1099511627776).toFixed(1)) + ' TB';
    if (v >= 1073741824) return trimZero((v / 1073741824).toFixed(1)) + ' GB';
    if (v >= 1048576) return trimZero((v / 1048576).toFixed(1)) + ' MB';
    if (v >= 1024) return trimZero((v / 1024).toFixed(1)) + ' KB';
    return v + ' B';
  }

  function percent(value, digits) {
    if (!isNumber(value)) return '0%';
    var v = Number(value);
    if (v > 100) v = 100;
    if (v < 0) v = 0;
    return v.toFixed(digits === undefined ? 0 : digits) + '%';
  }

  /** 水位等级：bad ≥ 80，warn ≥ 65，否则 ok */
  function level(value) {
    var v = Number(value) || 0;
    return v >= 80 ? 'bad' : v >= 65 ? 'warn' : 'ok';
  }

  /** 状态药丸等级 */
  function statusLevel(percentOfOnline, alarmCount) {
    if (Number(alarmCount) > 0) return 'warn';
    var v = Number(percentOfOnline);
    if (!isNumber(v)) return 'muted';
    return v >= 99.5 ? 'online' : v >= 90 ? 'warn' : 'offline';
  }

  function pad(n) {
    return n < 10 ? '0' + n : String(n);
  }

  function timeAgo(value) {
    if (!value) return '—';
    var t = new Date(value);
    if (Number.isNaN(t.getTime())) return '—';
    var diff = (Date.now() - t.getTime()) / 1000;
    if (diff < 0) diff = 0;
    if (diff < 60) return '刚刚';
    if (diff < 3600) return Math.floor(diff / 60) + ' 分钟前';
    if (diff < 86400) return Math.floor(diff / 3600) + ' 小时前';
    if (diff < 604800) return Math.floor(diff / 86400) + ' 天前';
    return t.getFullYear() + '-' + pad(t.getMonth() + 1) + '-' + pad(t.getDate());
  }

  function clockText(date) {
    var d = date || new Date();
    return pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
  }

  /** KPI 迷你趋势：SVG 折线 + 渐变面积 */
  var sparkUid = 0;
  function spark(values, color) {
    var list = (values || []).filter(isNumber).map(Number);
    if (list.length < 2) list = [0, 0];
    var c = color || 'var(--sd-color-brand)';
    var width = 100;
    var height = 32;
    var min = Math.min.apply(null, list);
    var max = Math.max.apply(null, list);
    var span = max - min || 1;
    var step = width / (list.length - 1 || 1);
    var pts = list.map(function (v, i) {
      return [i * step, height - 3 - ((v - min) / span) * (height - 7)];
    });
    var line = '';
    pts.forEach(function (p, i) {
      line += (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1) + ' ';
    });
    var area = line + 'L ' + width + ' ' + height + ' L 0 ' + height + ' Z';
    var gid = 'sdg' + ++sparkUid;
    return '<svg viewBox="0 0 ' + width + ' ' + height + '" preserveAspectRatio="none" width="100%" height="100%">' +
      '<defs><linearGradient id="' + gid + '" x1="0" y1="0" x2="0" y2="1">' +
      '<stop offset="0%" stop-color="' + c + '" stop-opacity=".34"/>' +
      '<stop offset="100%" stop-color="' + c + '" stop-opacity="0"/>' +
      '</linearGradient></defs>' +
      '<path d="' + area + '" fill="url(#' + gid + ')"/>' +
      '<path d="' + line + '" fill="none" stroke="' + c + '" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>' +
      '</svg>';
  }

  /* ============================== 图表 ============================== */

  var PALETTES = {
    dark: {
      brand: '#6673FF', brand2: '#8C5CF5', accent: '#6FE3EC',
      online: '#43E8A3', alert: '#FFC208', offline: '#FF4545',
      text: '#E6EDF3', regular: '#C7D0DA', secondary: '#8A97A6',
      grid: 'rgba(58, 68, 80, .42)', axisLine: '#33414E', border: '#3A4450',
      tooltipBg: '#26333F', surface: '#1F2A33',
      areaTop: 0.34, areaBottom: 0.02, barTop: 0.95, barBottom: 0.35,
      shadow: 'rgba(102, 115, 255, .35)',
      radarArea: ['rgba(102, 115, 255, .03)', 'rgba(102, 115, 255, .07)']
    },
    light: {
      brand: '#4F5BE8', brand2: '#7A46E0', accent: '#0E8F9B',
      online: '#0B9A66', alert: '#B45309', offline: '#D92D20',
      text: '#16202A', regular: '#2B3A48', secondary: '#5C6B7A',
      grid: 'rgba(22, 32, 42, .08)', axisLine: '#D7E0EA', border: '#D7E0EA',
      tooltipBg: '#FFFFFF', surface: '#FFFFFF',
      areaTop: 0.22, areaBottom: 0.01, barTop: 0.95, barBottom: 0.62,
      shadow: 'rgba(79, 91, 232, .16)',
      radarArea: ['rgba(79, 91, 232, .03)', 'rgba(79, 91, 232, .07)']
    }
  };

  function palette() {
    return document.documentElement.dataset.theme === 'light' ? PALETTES.light : PALETTES.dark;
  }

  function styles() {
    var p = palette();
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

  var charts = [];

  function fallback(el) {
    el.innerHTML = '<div class="chart-fallback">图表库未加载<br>（离线环境请检查 ECharts 静态资源）</div>';
  }

  function init(id, option) {
    var el = typeof id === 'string' ? document.getElementById(id) : id;
    if (!el) return null;
    if (!window.echarts) {
      fallback(el);
      return null;
    }
    var previous = window.echarts.getInstanceByDom(el);
    if (previous) {
      previous.dispose();
      var idx = charts.indexOf(previous);
      if (idx >= 0) charts.splice(idx, 1);
    }
    var chart = window.echarts.init(el, null, { renderer: 'canvas' });
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

  /** 主趋势：请求量面积 + 次要指标折线（双轴） */
  function areaTrend(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
    var series = [{
      name: o.primary.name || '请求量',
      type: 'line',
      smooth: true,
      symbol: 'none',
      yAxisIndex: 0,
      lineStyle: { width: 2.4, color: o.primary.color || p.brand },
      itemStyle: { color: o.primary.color || p.brand },
      areaStyle: { color: gradient(o.primary.color || p.brand, p.areaTop, p.areaBottom) },
      data: o.primary.data
    }];
    if (o.secondary) {
      series.push({
        name: o.secondary.name,
        type: o.secondary.type || 'line',
        smooth: true,
        symbol: 'none',
        yAxisIndex: 1,
        lineStyle: { width: 1.8, color: o.secondary.color || p.alert },
        itemStyle: { color: o.secondary.color || p.alert },
        areaStyle: o.secondary.bar ? undefined : { color: gradient(o.secondary.color || p.alert, p.areaTop * 0.4, 0) },
        data: o.secondary.data
      });
    }

    var yAxis = [{
      type: 'value',
      name: o.primary.unit || '',
      nameTextStyle: { color: p.secondary, fontSize: 10, padding: [0, 0, 0, 4] },
      axisLabel: Object.assign({}, t.axisLabel, { formatter: o.primary.formatter }),
      splitLine: t.splitLine,
      axisLine: { show: false }
    }];
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

  /** 横向条形榜（Top 应用 / 端点） */
  function barH(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
    var items = o.items || [];
    var max = Math.max.apply(null, items.map(function (i) { return i.value; }).concat([1]));
    var chart = init(id, {
      animationDuration: 600,
      grid: { left: 4, right: 46, top: 6, bottom: 2, containLabel: true },
      tooltip: Object.assign({}, t.tooltip, {
        trigger: 'item',
        formatter: function (item) {
          var value = o.format ? o.format(item.value) : fmt(item.value);
          return item.name + '：' + value + (o.unit || '');
        }
      }),
      xAxis: { type: 'value', max: max * 1.08, show: false },
      yAxis: {
        type: 'category',
        inverse: true,
        data: items.map(function (i) { return i.name; }),
        axisLabel: { color: p.regular, fontSize: 11, width: 118, overflow: 'truncate' },
        axisLine: { show: false },
        axisTick: { show: false }
      },
      series: [{
        type: 'bar',
        data: items.map(function (i) {
          return {
            value: i.value,
            url: i.url,
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
          formatter: function (item) { return o.format ? o.format(item.value) : fmt(item.value); }
        }
      }]
    });

    if (chart && (o.onClick || items.some(function (i) { return i.url; }))) {
      chart.on('click', function (params) {
        var item = items[params.dataIndex];
        if (item && item.url) location.href = item.url;
        else if (o.onClick) o.onClick(params);
      });
    }
    return chart;
  }

  /** 环形占比（集群健康 / 错误分布） */
  function donut(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
    var chart = init(id, {
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
        minAngle: 2,
        avoidLabelOverlap: true,
        itemStyle: { borderColor: p.surface, borderWidth: 3 },
        label: { show: false },
        labelLine: { show: false },
        data: (o.data || []).map(function (item) {
          return Object.assign({}, item, { url: item.url });
        })
      }]
    });

    if (chart && (o.onClick || (o.data || []).some(function (item) { return item.url; }))) {
      chart.on('click', function (params) {
        var item = (o.data || [])[params.dataIndex];
        if (item && item.url) location.href = item.url;
        else if (o.onClick) o.onClick(params);
      });
    }
    return chart;
  }

  /** 多线趋势（节点资源） */
  function multiLine(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
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

  /** 雷达（节点健康评分） */
  function radar(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
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

  /** 关系拓扑（预设坐标，避免力导向抖动） */
  function graph(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
    var categories = o.categories || [];
    var nodes = o.nodes || [];

    // ECharts 连线按节点 name 关联，节点的 key 需先映射成 name
    var keyMap = {};
    nodes.forEach(function (n) {
      if (n.key) keyMap[n.key] = n.name;
      if (n.id) keyMap[n.id] = n.name;
    });

    var chart = init(id, {
      animationDuration: 800,
      tooltip: Object.assign({}, t.tooltip, {
        trigger: 'item',
        formatter: function (item) {
          if (item.dataType === 'edge') return item.data.source + ' → ' + item.data.target;
          var value = o.format ? o.format(item.value) : fmt(item.value);
          return item.name + '：' + value + (o.unit || '');
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
        lineStyle: { color: p.brand, opacity: 0.55, width: 1.4, curveness: 0.08 },
        emphasis: {
          focus: 'adjacency',
          lineStyle: { color: p.accent, width: 2 },
          label: { color: p.text }
        },
        categories: categories,
        data: nodes.map(function (n) {
          var c = categories[n.category || 0];
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
            source: keyMap[l.source] || l.source,
            target: keyMap[l.target] || l.target,
            lineStyle: { width: Math.min(3, 0.6 + (l.value || 1) / 60) }
          });
        })
      }]
    });

    if (chart && o.onClick) chart.on('click', function (params) {
      if (params.dataType === 'node' && params.data && params.data.url) location.href = params.data.url;
    });
    return chart;
  }

  /** 堆叠 / 分组柱（机房资源） */
  function stackedBar(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
    var chart = init(id, {
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

    if (chart && o.urls && o.urls.length) {
      chart.on('click', function (params) {
        var url = o.urls[params.dataIndex];
        if (url) location.href = url;
      });
    }
    return chart;
  }

  /** 仪表盘（网络质量评分） */
  function gauge(id, opts) {
    var o = opts || {};
    var t = styles();
    var p = t.p;
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
        title: { show: true, offsetCenter: [0, '34%'], color: p.secondary, fontSize: 10 },
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

  function resizeAll() {
    charts.forEach(function (c) {
      if (c && !c.isDisposed()) c.resize();
    });
  }

  window.addEventListener('resize', resizeAll);

  /* ============================== 渲染注册 ============================== */

  var renderers = [];

  /** 注册图表/动态内容渲染函数。主题切换后会自动重跑 */
  function onRender(fn) {
    if (typeof fn === 'function') renderers.push(fn);
  }

  function rerender() {
    renderers.forEach(function (fn) {
      try {
        fn();
      } catch (e) {
        if (window.console) console.warn('[dashboard] render failed', e);
      }
    });
  }

  /** 刷新 KPI 卡片数值。服务端已格式化，前端只做定位替换 */
  function applyKpis(list) {
    (list || []).forEach(function (k) {
      if (!k || !k.key) return;
      var el = document.querySelector('[data-kpi="' + k.key + '"]');
      if (!el) return;

      var value = el.querySelector('[data-kpi-value]');
      if (value) value.textContent = k.value === undefined || k.value === null ? '—' : k.value;
      var unit = el.querySelector('[data-kpi-unit]');
      if (unit) unit.textContent = k.unit || '';
      var note = el.querySelector('[data-kpi-note]');
      if (note) note.textContent = k.note || '';
      var delta = el.querySelector('[data-kpi-delta]');
      if (delta) {
        delta.textContent = k.delta || '';
        delta.className = 'delta delta--' + (k.deltaLevel || 'flat');
        delta.hidden = !k.delta;
      }
      var sparkEl = el.querySelector('[data-kpi-spark]');
      if (sparkEl && k.spark && k.spark.length) {
        sparkEl.innerHTML = spark(k.spark, k.sparkColor || 'var(--sd-color-brand)');
      }
    });
  }

  function sparkInto(el, values, color) {
    var target = typeof el === 'string' ? document.querySelector(el) : el;
    if (target) target.innerHTML = spark(values, color);
  }

  /** 把长序列压缩到目标点数。逐段取均值，用于迷你趋势图 */
  function bucket(values, count) {
    var list = (values || []).filter(isNumber).map(Number);
    if (list.length <= count || count <= 0) return list;

    var size = list.length / count;
    var result = [];
    for (var i = 0; i < count; i++) {
      var start = Math.floor(i * size);
      var end = Math.max(start + 1, Math.floor((i + 1) * size));
      var sum = 0;
      for (var j = start; j < end && j < list.length; j++) sum += list[j];
      result.push(sum / (end - start));
    }
    return result;
  }

  /* ============================== 壳层 ============================== */

  function startClock() {
    function tick() {
      var text = clockText();
      document.querySelectorAll('[data-clock]').forEach(function (el) { el.textContent = text; });
    }
    tick();
    setInterval(tick, 1000);
  }

  /**
   * 自动刷新：按间隔拉取 JSON 并交给页面 apply 函数。
   * 页面隐藏时暂停，重新可见时立即刷新一次。
   */
  function poll(url, apply, interval) {
    var timer = null;
    var paused = false;
    var busy = false;

    function load() {
      if (busy || paused) return;
      busy = true;
      fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
        .then(function (r) {
          if (r.status === 401) {
            // 会话过期：整页跳转登录，避免静默失败
            if (!window.__sdAuthReload) {
              window.__sdAuthReload = true;
              location.reload();
            }
            throw new Error('unauthorized');
          }
          if (!r.ok) throw new Error('HTTP ' + r.status);
          return r.json();
        })
        .then(function (data) {
          try {
            apply(data);
            document.querySelectorAll('[data-last-update]').forEach(function (el) {
              el.textContent = clockText();
            });
          } catch (e) {
            if (window.console) console.warn('[dashboard] apply failed', e);
          }
        })
        .catch(function (e) {
          if (window.console) console.warn('[dashboard] refresh failed', e);
        })
        .then(function () { busy = false; });
    }

    function resume() {
      paused = false;
      load();
      if (timer) clearInterval(timer);
      timer = setInterval(load, interval || 30000);
    }

    function pause() {
      paused = true;
      if (timer) clearInterval(timer);
      timer = null;
    }

    document.addEventListener('visibilitychange', function () {
      if (document.hidden) pause();
      else resume();
    });

    resume();
    return { load: load, pause: pause, resume: resume };
  }

  /** 把内联 onclick 的行链接转成可键盘访问的 data-href */
  function enhanceRowLinks() {
    document.querySelectorAll('.row-link[onclick]').forEach(function (el) {
      if (el.dataset.href) return;
      var code = el.getAttribute('onclick') || '';
      var match = code.match(/location\.href\s*=\s*['"]([^'"]+)['"]/);
      if (!match) return;

      el.dataset.href = match[1];
      el.removeAttribute('onclick');
      el.setAttribute('tabindex', '0');
      el.setAttribute('role', 'link');
    });
  }

  function updateAlarmActions(row, action, res) {
    if (!row) return;

    var link = row.querySelector('.alarm-actions__link');
    var state = document.createElement('span');
    state.className = 'alarm-actions__state';

    if (action === 'ack') {
      var ack = row.querySelector('[data-alarm-action="ack"]');
      if (ack) ack.remove();
      state.textContent = (res && res.ackUser ? res.ackUser + ' ' : '') + '已确认';
    } else {
      var select = row.querySelector('[data-alarm-hours]');
      if (select) select.remove();
      var silence = row.querySelector('[data-alarm-action="silence"]');
      if (silence) silence.remove();
      state.textContent = '静默至 ' + ((res && res.silenceTime) || '');
    }

    row.insertBefore(state, link || null);
  }

  function bindAlarmActions() {
    document.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-alarm-action]');
      if (!btn) return;

      var action = btn.dataset.alarmAction;
      var id = btn.dataset.alarmId;
      if (!id) return;

      var row = btn.closest('[data-alarm-actions]');
      var hours = 24;
      if (action === 'silence' && row) {
        var select = row.querySelector('[data-alarm-hours]');
        if (select) hours = Number(select.value) || 24;
      }

      var body = 'id=' + encodeURIComponent(id);
      if (action === 'silence') body += '&hours=' + encodeURIComponent(hours);

      var token = document.querySelector('input[name="__RequestVerificationToken"]');
      btn.disabled = true;

      fetch(action === 'ack' ? '/Dashboard/Alarm/Ack' : '/Dashboard/Alarm/Silence', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
          'RequestVerificationToken': token ? token.value : ''
        },
        body: body
      }).then(function (r) {
        if (r.status === 401) {
          location.reload();
          throw new Error('登录已过期');
        }
        if (!r.ok) {
          return r.json().catch(function () { return {}; }).then(function (x) {
            throw new Error(x.message || ('HTTP ' + r.status));
          });
        }
        return r.json();
      }).then(function (res) {
        updateAlarmActions(row, action, res);
      }).catch(function (err) {
        btn.disabled = false;
        if (window.alert) alert(err.message || '操作失败');
      });
    });
  }

  function bindShell() {
    startClock();
    renderThemeSwitches();
    bindAlarmActions();

    document.querySelectorAll('[data-action="refresh"]').forEach(function (btn) {
      btn.addEventListener('click', function () { location.reload(); });
    });

    document.addEventListener('click', function (e) {
      var kpi = e.target.closest('[data-kpi-url]');
      if (kpi && kpi.dataset.kpiUrl) {
        location.href = kpi.dataset.kpiUrl;
        return;
      }

      var row = e.target.closest('[data-href]');
      if (row && !e.target.closest('a,button,input,select,textarea')) location.href = row.dataset.href;
    });

    document.addEventListener('keydown', function (e) {
      if (e.key !== 'Enter' && e.key !== ' ') return;

      var kpi = e.target.closest('[data-kpi-url]');
      if (kpi && kpi.dataset.kpiUrl) {
        e.preventDefault();
        location.href = kpi.dataset.kpiUrl;
        return;
      }

      var row = e.target.closest('[data-href]');
      if (row && e.target === row) {
        e.preventDefault();
        location.href = row.dataset.href;
      }
    });

    document.addEventListener('DOMContentLoaded', function () {
      enhanceRowLinks();
      renderThemeSwitches();
      rerender();
    });
  }

  bindShell();

  /* ============================== 导出 ============================== */

  window.SD = {
    fmt: fmt,
    compact: compact,
    fixed: fixed,
    bytes: bytes,
    percent: percent,
    level: level,
    statusLevel: statusLevel,
    timeAgo: timeAgo,
    spark: spark,
    bucket: bucket,
    isDark: function () { return document.documentElement.dataset.theme !== 'light'; },
    theme: {
      mode: storedMode,
      resolved: function () { return resolveMode(storedMode()); },
      set: setTheme,
      apply: applyTheme
    },
    onRender: onRender,
    rerender: rerender,
    applyKpis: applyKpis,
    sparkInto: sparkInto,
    poll: poll,
    enhanceRowLinks: enhanceRowLinks
  };

  window.SDCharts = {
    areaTrend: areaTrend,
    barH: barH,
    donut: donut,
    multiLine: multiLine,
    radar: radar,
    graph: graph,
    stackedBar: stackedBar,
    gauge: gauge,
    init: init,
    gradient: gradient,
    resizeAll: resizeAll
  };

  Object.defineProperty(window.SDCharts, 'palette', { get: palette, enumerable: true });
  Object.defineProperty(window.SDCharts, 'styles', { get: styles, enumerable: true });
})();
