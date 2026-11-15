<script setup lang="ts">
// 节点搜索 Lov（CARD-NODE-07 §9.3，与 NODE-01 的 AppLov 同形）：远程联想 el-autocomplete，
// 契约 GET /Nodes/Node/NodeSearch（key/pageSize 20/projectId）。data-spec 锚挂外层包裹 div
// （el-autocomplete inheritAttrs:false，attrs 透传到 DOM 不可靠——5194 CT 实证）。
import { ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import { fetchNodeSearchOptions, type NodeOption } from './nodesPage.logic.ts';

const props = defineProps<{
  /** 联想取数（注入优先；缺省走 NodeSearch 真实端点，projectId 取当前上下文） */
  fetchOptions?: (key: string) => Promise<NodeOption[]>;
  placeholder?: string;
}>();

const emit = defineEmits<{ select: [item: { id: number; value: string }] }>();

const kw = ref('');
// 预绑 request 与 projectId，收敛 fetchOptions 签名为 (key) => Promise<NodeOption[]>
const fetchOptionsFn = props.fetchOptions ?? ((key: string) => fetchNodeSearchOptions(request, key, stardustContext.projectId));

function querySearch(query: string, cb: (list: NodeOption[]) => void): void {
  fetchOptionsFn(query).then((list) => cb(list));
}

function onSelect(item: NodeOption): void {
  if (item && item.id > 0) emit('select', { id: item.id, value: item.value });
  kw.value = ''; // 选中后清空输入框（AppLov 同款）
}
</script>

<template>
  <div
    class="w-full"
    data-spec="node-data-lov-input"
  >
    <el-autocomplete
      v-model="kw"
      :fetch-suggestions="querySearch"
      :trigger-on-focus="false"
      :debounce="300"
      :placeholder="placeholder ?? '搜索节点：名称 / 编码 / IP'"
      clearable
      value-key="value"
      class="w-full"
      @select="onSelect"
    >
      <template #default="{ item }">
        <div
          data-spec="node-data-lov-option"
          class="flex items-center gap-2 truncate"
        >
          <span class="truncate">{{ item.value }}</span>
          <span
            class="truncate text-xs text-[--el-text-color-secondary]"
          >#{{ item.id }}</span>
        </div>
      </template>
    </el-autocomplete>
  </div>
</template>
