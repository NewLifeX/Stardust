<script setup lang="ts">
/**
 * DeployNodes：发布页「部署节点」，对应 ShipPanel 原模板的第 3 块（2026-10-28 拆分）。
 * 纯 UI：只收 props、只发 operate(node, action)；二次确认（openGuard）与真正下发（operate）在容器里。
 * 节点版本与部署集版本的比对标签由容器用 nodeVersionTag 算好，按节点行 id 传入，本组件不依赖部署集。
 * 下发中的按钮写「下发中…」，且有操作进行时所有按钮禁用，避免并发下发。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { VersionTag } from './deployView.logic.ts';
import { NODE_ACTIONS } from './ship.logic.ts';
import type { NodeActionInfo, ShipNode } from './ship.logic.ts';

defineProps<{
  nodes: ShipNode[];
  /** 节点加载失败：底部写失败而不是「暂无节点」 */
  nodesFailed: boolean;
  /** 正在进行的操作 key，形如 `<节点行Id>:<动作>`；非空时所有按钮禁用 */
  busyKey: string;
  /** 按节点行 id 索引的版本标签；没有（停用节点）则不显示标签 */
  versionTags: Record<number, VersionTag | null | undefined>;
}>();

const emit = defineEmits<{ operate: [node: ShipNode, action: NodeActionInfo] }>();
</script>

<template>
  <Card
    padding="none"
    data-spec="ship-nodes"
  >
    <div class="px-[18px] pb-2 pt-4">
      <h3 :class="H3">
        部署节点
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">对单个节点安装、启动、重启、停止、卸载；命令只表示已发出，是否生效看版本与部署历史</span>
      </h3>
    </div>
    <table class="w-full border-collapse">
      <thead>
        <tr>
          <th :class="TH">
            节点
          </th>
          <th :class="TH">
            模式
          </th>
          <th :class="TH">
            当前版本
          </th>
          <th :class="TH">
            状态
          </th>
          <th :class="[TH, 'text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="n in nodes"
          :key="n.id"
          data-spec="ship-node-row"
          class="group"
        >
          <td :class="TD">
            <b class="font-medium text-[var(--el-text-color-primary)]">{{ n.name }}</b>
            <div class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
              {{ n.ip || '—' }}
            </div>
          </td>
          <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
            {{ n.modeLabel }}
          </td>
          <td :class="TD">
            <span class="font-mono tabular-nums">{{ n.version || '—' }}</span>
            <Tag
              v-if="versionTags[n.id]"
              :tone="versionTags[n.id]?.tone"
              class="ml-2"
            >
              {{ versionTags[n.id]?.label }}
            </Tag>
          </td>
          <td :class="TD">
            <Tag :tone="n.enable ? 'ok' : 'default'">
              {{ n.enable ? '启用' : '已停用' }}
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <div class="inline-flex flex-wrap justify-end gap-[5px]">
              <Btn
                v-for="a in NODE_ACTIONS"
                :key="a.act"
                size="sm"
                :variant="a.danger ? 'danger' : 'default'"
                :disabled="!!busyKey"
                :data-spec="`ship-op-${a.act}`"
                @click="emit('operate', n, a)"
              >
                {{ busyKey === `${n.id}:${a.act}` ? '下发中…' : a.label }}
              </Btn>
            </div>
          </td>
        </tr>
        <tr v-if="nodesFailed">
          <td
            colspan="5"
            class="p-5 text-[12.5px] text-[var(--el-color-danger)]"
            data-spec="ship-nodes-failed"
          >
            节点加载失败，请点上方「刷新」重试
          </td>
        </tr>
        <tr v-else-if="!nodes.length">
          <td
            colspan="5"
            class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="ship-nodes-empty"
          >
            该部署集下暂无节点
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
