<script setup lang="ts">
/**
 * BuildNodes：编译节点表格（纯 UI）。
 * 对应后端 AppBuildNode：在节点上拉取源代码、编译项目、打包输出、上传应用包。
 * 只收 props、只发 operate(row, act)；二次确认（openGuard）与真正下发在容器里。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import { BUILD_NODE_ACTIONS, fmtBuildTime } from './buildNodes.logic';
import type { BuildNodeAct, BuildNodeRow } from './buildNodes.logic';

defineProps<{
  rows: BuildNodeRow[];
  /** 加载失败：底部写失败而不是空态 */
  failed: boolean;
  /** 正在进行的操作 key，形如 `<行Id>:<act>`；非空时所有按钮禁用 */
  busyKey: string;
}>();

const emit = defineEmits<{ operate: [row: BuildNodeRow, act: BuildNodeAct] }>();

function checkClass(v: boolean): string {
  return v ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-placeholder)]';
}
</script>

<template>
  <Card
    padding="none"
    data-spec="ship-build-nodes"
  >
    <div class="px-[18px] pb-2 pt-4">
      <h3 :class="H3">
        编译节点
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">在节点上拉取源代码、编译项目、打包并上传应用包</span>
      </h3>
    </div>
    <table class="w-full border-collapse">
      <thead>
        <tr>
          <th :class="TH">
            节点名
          </th>
          <th :class="TH">
            启用
          </th>
          <th :class="TH">
            源代码目录
          </th>
          <th :class="TH">
            拉取源代码
          </th>
          <th :class="TH">
            编译项目
          </th>
          <th :class="TH">
            打包输出
          </th>
          <th :class="TH">
            上传应用包
          </th>
          <th :class="TH">
            输出目录
          </th>
          <th :class="TH">
            更新时间
          </th>
          <th :class="[TH, 'text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="build-node-row"
          class="group"
        >
          <td :class="TD">
            <b class="font-medium text-[var(--el-text-color-primary)]">{{ r.nodeName }}</b>
            <div class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
              {{ r.nodeIp || `#${r.nodeId}` }}
            </div>
          </td>
          <td :class="TD">
            <KIcon
              v-if="r.enable"
              name="Check"
              :size="15"
              class="text-[var(--el-color-success)]"
            />
            <KIcon
              v-else
              name="X"
              :size="15"
              class="text-[var(--el-text-color-placeholder)]"
            />
          </td>
          <td :class="[TD, 'font-mono text-[11.5px] text-[var(--el-text-color-secondary)]']">
            {{ r.sourcePath || '—' }}
          </td>
          <td :class="TD">
            <KIcon
              :name="r.pullCode ? 'Check' : 'X'"
              :size="15"
              :class="checkClass(r.pullCode)"
            />
          </td>
          <td :class="TD">
            <KIcon
              :name="r.buildProject ? 'Check' : 'X'"
              :size="15"
              :class="checkClass(r.buildProject)"
            />
          </td>
          <td :class="TD">
            <KIcon
              :name="r.packageOutput ? 'Check' : 'X'"
              :size="15"
              :class="checkClass(r.packageOutput)"
            />
          </td>
          <td :class="TD">
            <KIcon
              :name="r.uploadPackage ? 'Check' : 'X'"
              :size="15"
              :class="checkClass(r.uploadPackage)"
            />
          </td>
          <td :class="[TD, 'font-mono text-[11.5px] text-[var(--el-text-color-secondary)]']">
            {{ r.outputPath || '—' }}
          </td>
          <td :class="[TD, 'font-mono tabular-nums text-[11.5px] text-[var(--el-text-color-secondary)]']">
            {{ fmtBuildTime(r.updateTime) }}
          </td>
          <td :class="[TD, 'text-right']">
            <div class="inline-flex flex-wrap justify-end gap-[5px]">
              <Btn
                v-for="a in BUILD_NODE_ACTIONS"
                :key="a.act"
                size="sm"
                :disabled="!!busyKey"
                :data-spec="`build-op-${a.act}`"
                @click="emit('operate', r, a.act)"
              >
                {{ busyKey === `${r.id}:${a.act}` ? '处理中…' : a.label }}
              </Btn>
            </div>
          </td>
        </tr>
        <tr v-if="failed">
          <td
            colspan="10"
            class="p-5 text-[12.5px] text-[var(--el-color-danger)]"
            data-spec="build-nodes-failed"
          >
            编译节点加载失败，请点上方刷新重试
          </td>
        </tr>
        <tr v-else-if="!rows.length">
          <td
            colspan="10"
            class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="build-nodes-empty"
          >
            该部署集还没有配置编译节点
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
