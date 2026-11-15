<script setup lang="ts">
/**
 * AppLov：应用联想搜索框（CARD-DEP-08 D5，J-REG-01 §8 行 9 移交项，候选 A 定稿）。
 * 对照旧壳 /Registry/App 顶部 el-autocomplete（apps/stardust/src/views/Registry/App/index.vue:200）：
 * 远程搜 GET /Registry/App/AppSearch?key=（enable 服务端固定 true，PageSize 20），选中后由父级跳到该应用的部署页签。
 * 数据函数由 props 注入（默认 fetchAppOptions + toLovItem），单测/CT 可换替身；防抖 300ms 由 el-autocomplete 内置承担。
 */
import { ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { fetchAppOptions, toLovItem } from './ship.logic.ts';
import type { AppOption } from './ship.logic.ts';

const props = withDefaults(
  defineProps<{
    /** 远程联想函数（默认真实接口；测试注入替身） */
    fetchOptions?: (key: string) => Promise<AppOption[]>;
    placeholder?: string;
  }>(),
  { placeholder: '输入应用名切换部署集' },
);

const emit = defineEmits<{ select: [item: { value: string; id: number }] }>();

const kw = ref('');
// 默认接真实接口：fetchAppOptions 签名是 (request, key)，这里预绑 request 收敛成 (key)，
// 避免与注入替身的 (key) 联合后调用端参数个数不匹配（TS2554）
const fetchOptionsFn: (key: string) => Promise<AppOption[]> =
  props.fetchOptions ?? ((key: string) => fetchAppOptions(request, key));

async function querySearch(query: string, cb: (items: { value: string; id: number }[]) => void) {
  const list = await fetchOptionsFn(query);
  cb(list.map(toLovItem));
}

function onSelect(item: { value: string; id: number }) {
  if (item?.id > 0) emit('select', item);
  kw.value = '';
}
</script>

<template>
  <!-- data-spec 放外层 wrapper：el-autocomplete 是 inheritAttrs:false（EP autocomplete2.mjs），
       attrs 经 mergeProps 进内部 el-input 链，透传到 DOM 不可靠（5194 CT 实证 30s 找不到锚） -->
  <div data-spec="deploy-app-lov">
    <el-autocomplete
      v-model="kw"
      class="!w-[320px]"
      :fetch-suggestions="querySearch"
      :trigger-on-focus="false"
      :debounce="300"
      :placeholder="placeholder"
      clearable
      value-key="value"
      @select="onSelect"
    />
  </div>
</template>
