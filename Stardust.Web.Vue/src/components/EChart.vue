<script setup lang="ts">
/**
 * EChart.vue — 通用 ECharts 图表组件（图表基建）
 *
 * 使用方式（所有微应用视图均可用 @/components/EChart.vue）：
 *   import EChart from '@/components/EChart.vue';
 *   <EChart :option="chartOption" height="360px" />
 *
 * 约定：
 *  - props.option: echarts option 对象（必需）
 *  - props.height: 容器高度，默认 '360px'；宽度 100%
 *  - 颜色不硬编码品牌色，series 默认使用一组中性灰蓝调色板（可在 option.color 覆盖）
 *  - 生命周期：onMounted init + setOption；window resize 自动 resize；onUnmounted dispose
 *  - option 变化时（外部换数据/配置）自动 setOption（notMerge 保证系列数量变化时干净重绘）
 */
import { ref, shallowRef, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
import * as echarts from 'echarts';

const props = withDefaults(
  defineProps<{
    option: Record<string, unknown>;
    height?: string;
  }>(),
  {
    height: '360px',
  },
);

// 中性灰蓝调色板（避免依赖 --el-* 品牌色，也不写死高饱和品牌色）
const NEUTRAL_PALETTE = [
  '#5B8DB8', // 灰蓝
  '#7A9E9F', // 青灰
  '#B08C6A', // 暖褐
  '#8E8E8E', // 中性灰
  '#6B7B8C', // 蓝灰
  '#A3A380', // 橄榄灰
  '#9C7CA5', // 灰紫
  '#5F8D7E', // 墨绿灰
];

const containerRef = ref<HTMLDivElement | null>(null);
const chartRef = shallowRef<echarts.ECharts | null>(null);

function buildOption(): Record<string, unknown> {
  // 不覆盖外部已显式指定的 color
  const base: Record<string, unknown> = { color: NEUTRAL_PALETTE };
  return { ...base, ...props.option };
}

function render() {
  const el = containerRef.value;
  if (!el) return;
  if (!chartRef.value) {
    chartRef.value = echarts.init(el);
  }
  chartRef.value.setOption(buildOption(), { notMerge: true });
}

function handleResize() {
  chartRef.value?.resize();
}

onMounted(async () => {
  await nextTick();
  render();
  window.addEventListener('resize', handleResize);
});

// option 变化（换数据/配置）自动重绘
watch(
  () => props.option,
  () => {
    if (chartRef.value) render();
    else nextTick(render);
  },
  { deep: true },
);

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize);
  chartRef.value?.dispose();
  chartRef.value = null;
});
</script>

<template>
  <div
    ref="containerRef"
    class="e-chart"
    :style="{ width: '100%', height }"
  />
</template>

<style scoped>
.e-chart {
  width: 100%;
}
</style>
