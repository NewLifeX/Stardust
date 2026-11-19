<script setup lang="ts">
/**
 * DeployNodes：发布页「部署节点」，对应 ShipPanel 原模板的第 3 块（2026-10-28 拆分；含批量远程操作）。
 * 纯 UI：只收 props、只发 operate/batch/create/edit/remove；二次确认（openGuard）与真正下发在容器里。
 * 勾选是本组件内的展示状态：批量栏随勾选出现，批量按钮 emit('batch', action, ids)，容器按 nodes 找名字。
 * 下发中的按钮写「下发中…」，且有操作进行时所有按钮禁用，避免并发下发（AGENTS.md §11）。
 */
import { computed, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { VersionTag } from './deployView.logic.ts';
import { NODE_ACTIONS } from './ship.logic.ts';
import type { DeployOthers, NodeActionInfo, ShipNode } from './ship.logic.ts';

const props = defineProps<{
  nodes: ShipNode[];
  /** 节点加载失败：底部写失败而不是「暂无节点」 */
  nodesFailed: boolean;
  /** 正在进行的操作 key，形如 `<节点行Id>:<动作>`；非空时所有按钮禁用 */
  busyKey: string;
  /** 按节点行 id 索引的版本标签；没有（停用节点）则不显示标签 */
  versionTags: Record<number, VersionTag | null | undefined>;
  /** 当前部署集名称（CARD-DEP-08 D4 空态主句用） */
  deployName?: string;
  /** CARD-DEP-08 D2：同应用其余部署集摘要；count>0 才显示副行（单集应用不显示） */
  others?: DeployOthers | null;
}>();

const emit = defineEmits<{
  operate: [node: ShipNode, action: NodeActionInfo];
  batch: [action: NodeActionInfo, ids: number[]];
  create: [];
  edit: [node: ShipNode];
  remove: [node: ShipNode];
  viewLogs: [node: ShipNode];
}>();

const selected = ref<number[]>([]);
const allChecked = computed(() => props.nodes.length > 0 && selected.value.length === props.nodes.length);

// 数据行变化（刷新后行可能增删）时清掉失效的勾选
watch(
  () => props.nodes,
  (rows) => {
    const ids = new Set(rows.map((r) => r.id));
    selected.value = selected.value.filter((id) => ids.has(id));
  },
);

function isChecked(id: number): boolean {
  return selected.value.includes(id);
}

function toggle(id: number): void {
  selected.value = isChecked(id) ? selected.value.filter((x) => x !== id) : [...selected.value, id];
}

function toggleAll(): void {
  selected.value = allChecked.value ? [] : props.nodes.map((r) => r.id);
}

function checkedNames(): string {
  return props.nodes
    .filter((n) => selected.value.includes(n.id))
    .map((n) => n.name)
    .join('、');
}
</script>

<template>
  <Card
    padding="none"
    data-spec="ship-nodes"
  >
    <div class="px-[18px] pb-2 pt-4">
      <div class="flex flex-wrap items-center gap-3">
        <h3 :class="H3">
          部署节点
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">对单个节点安装、启动、重启、停止、卸载；命令只表示已发出，是否生效看版本与部署历史</span>
        </h3>
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="primary"
          :disabled="!!busyKey"
          data-spec="ship-node-create"
          title="把一台物理节点加入本部署集"
          @click="emit('create')"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="12"
            />
          </template>
          新增节点
        </Btn>
      </div>
      <!-- 批量操作栏：勾选后出现在视线内，按钮写明将操作哪些节点 -->
      <div
        v-if="selected.length"
        class="mt-2 flex flex-wrap items-center gap-[6px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-primary)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_8%,transparent)] px-3 py-2"
        data-spec="ship-node-batchbar"
      >
        <span class="text-[12px] text-[var(--el-text-color-primary)]">
          已选 <b class="tabular-nums">{{ selected.length }}</b> 个节点（{{ checkedNames() }}），批量：
        </span>
        <Btn
          v-for="a in NODE_ACTIONS"
          :key="`batch-${a.act}`"
          size="sm"
          :variant="a.danger ? 'danger' : 'default'"
          :disabled="!!busyKey"
          :data-spec="`ship-node-batch-${a.act}`"
          @click="emit('batch', a, [...selected])"
        >
          {{ busyKey === `batch:${a.act}` ? '下发中…' : a.label }}
        </Btn>
        <Btn
          size="sm"
          variant="ghost"
          data-spec="ship-node-batch-clear"
          @click="selected = []"
        >
          取消选择
        </Btn>
      </div>
    </div>
    <table class="w-full border-collapse">
      <thead>
        <tr>
          <th :class="[TH, 'w-[34px]']">
            <input
              type="checkbox"
              :aria-label="allChecked ? '取消全选' : '全选节点'"
              data-spec="ship-node-check-all"
              :checked="allChecked"
              @change="toggleAll"
            >
          </th>
          <th :class="TH">
            节点
          </th>
          <th :class="TH">
            模式
          </th>
          <!-- task-92 列扩展：延迟/状态/进程/进程时间 + 编译时间/最后活跃（列序对齐用户令八列 IP/延迟/启用/进程/进程时间/版本/编译时间/最后活跃；MVC 对照 Stardust.Web/Areas/Deployment/Views/AppDeployNode/_List_Data.cshtml:37-44） -->
          <th
            :class="[TH, 'text-right']"
            title="延迟。发布下去后，延迟执行时间，单位秒（滚动发布用）"
          >
            延迟
          </th>
          <th :class="TH">
            状态
          </th>
          <th
            :class="[TH, 'text-right']"
            title="进程 ID。0 表示未上报或未运行"
          >
            进程
          </th>
          <th :class="TH">
            进程时间
          </th>
          <th :class="TH">
            当前版本
          </th>
          <!-- 2026-11-12 新列：应用程序版本=在线实例上报的应用入口程序集 FileVersion，随心跳更新；与「当前版本」（install 下发的部署版本）语义分开 -->
          <th
            :class="TH"
            title="应用程序版本。该节点上应用在线实例上报的文件版本（应用入口程序集FileVersion），随心跳自动更新"
          >
            应用程序版本
          </th>
          <th :class="TH">
            编译时间
          </th>
          <th
            :class="TH"
            title="最后一次上报心跳的时间"
          >
            最后活跃
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
            <input
              type="checkbox"
              :aria-label="`选择节点 ${n.name}`"
              :data-spec="`ship-node-check-${n.id}`"
              :checked="isChecked(n.id)"
              @change="toggle(n.id)"
            >
          </td>
          <td :class="TD">
            <b class="font-medium text-[var(--el-text-color-primary)]">{{ n.name }}</b>
            <div class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
              {{ n.ip || '—' }}
            </div>
          </td>
          <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
            {{ n.modeLabel }}
          </td>
          <td :class="[TD, 'text-right font-mono tabular-nums']">
            {{ n.delay }}
          </td>
          <td :class="TD">
            <Tag :tone="n.enable ? 'ok' : 'default'">
              {{ n.enable ? '启用' : '已停用' }}
            </Tag>
          </td>
          <td :class="[TD, 'text-right font-mono tabular-nums']">
            {{ n.processId || '—' }}
          </td>
          <td :class="[TD, 'whitespace-nowrap font-mono text-[12px] text-[var(--el-text-color-secondary)]']">
            {{ n.startTime }}
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
          <td :class="[TD, 'font-mono tabular-nums text-[var(--el-text-color-secondary)]']">
            {{ n.appVersion || '—' }}
          </td>
          <td :class="[TD, 'whitespace-nowrap font-mono text-[12px] text-[var(--el-text-color-secondary)]']">
            {{ n.compile }}
          </td>
          <td :class="[TD, 'whitespace-nowrap font-mono text-[12px] text-[var(--el-text-color-secondary)]']">
            {{ n.lastActive }}
          </td>
          <td :class="[TD, 'text-right']">
            <div class="inline-flex flex-wrap items-center justify-end gap-[5px]">
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
              <button
                type="button"
                data-spec="ship-node-logs"
                class="text-[12px] text-[var(--el-color-primary)] hover:underline disabled:pointer-events-none disabled:opacity-40"
                :disabled="!!busyKey"
                @click="emit('viewLogs', n)"
              >
                查看日志
              </button>
              <button
                type="button"
                data-spec="ship-node-edit"
                class="ml-1 text-[12px] text-[var(--el-color-primary)] hover:underline disabled:pointer-events-none disabled:opacity-40"
                :disabled="!!busyKey"
                @click="emit('edit', n)"
              >
                编辑
              </button>
              <button
                type="button"
                data-spec="ship-node-remove"
                class="text-[12px] text-[var(--el-color-danger)] hover:underline disabled:pointer-events-none disabled:opacity-40"
                :disabled="!!busyKey"
                @click="emit('remove', n)"
              >
                {{ busyKey === `node-del:${n.id}` ? '删除中…' : '删除' }}
              </button>
            </div>
          </td>
        </tr>
        <tr v-if="nodesFailed">
          <td
            colspan="12"
            class="p-5 text-[12.5px] text-[var(--el-color-danger)]"
            data-spec="ship-nodes-failed"
          >
            节点加载失败，请点上方「刷新」重试
          </td>
        </tr>
        <tr v-else-if="!nodes.length">
          <td
            colspan="12"
            class="p-5"
          >
            <!-- CARD-DEP-08 D4：空态主句点部署集名，引导按钮直接开新增抽屉 -->
            <div
              class="text-[12.5px] text-[var(--el-text-color-secondary)]"
              data-spec="deploy-nodes-empty"
            >
              部署集「{{ deployName || '—' }}」暂无节点
            </div>
            <Btn
              size="sm"
              variant="primary"
              class="mt-2"
              :disabled="!!busyKey"
              data-spec="deploy-nodes-empty-add"
              @click="emit('create')"
            >
              为该部署集新增节点
            </Btn>
            <div
              v-if="others && others.count > 0"
              class="mt-2 text-[12px] text-[var(--el-text-color-secondary)]"
              data-spec="deploy-nodes-others"
            >
              本应用另有 {{ others.count }} 个部署集（共 {{ others.nodes }} 个节点）
            </div>
          </td>
        </tr>
        <!-- CARD-DEP-08 D2：多集应用非空态副行（{m}=全部集启用节点数之和） -->
        <tr v-if="!nodesFailed && nodes.length && others && others.count > 0">
          <td
            colspan="12"
            class="px-5 pb-4 pt-1 text-[12px] text-[var(--el-text-color-secondary)]"
            data-spec="deploy-nodes-others"
          >
            本应用另有 {{ others.count }} 个部署集（共 {{ others.nodes }} 个节点）
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
