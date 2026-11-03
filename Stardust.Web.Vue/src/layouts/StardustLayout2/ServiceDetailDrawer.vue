<script setup lang="ts">
/**
 * ServiceDetailDrawer：服务详情。三段：Service 编辑表单（PUT /Registry/Service/{id}
 * 部分更新）+ 提供者表（/Registry/AppService?serviceId=）+ 消费者表（/Registry/AppConsume?serviceId=）。
 * 详情取数由容器做（失败显示加载失败）；表单校验 inline；保存中禁用（§11）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { validateServiceEdit } from './mesh.logic.ts';
import type { ServiceEditModel, ServiceMemberRow } from './mesh.logic.ts';
import { TD, TH } from './deployView.styles.ts';

const props = defineProps<{
  open: boolean;
  loading: boolean;
  /** 详情加载失败 */
  failed: boolean;
  service: ServiceEditModel | null;
  providers: ServiceMemberRow[];
  consumers: ServiceMemberRow[];
  serviceName: string;
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; retry: []; submit: [form: ServiceEditModel] }>();

const form = reactive<ServiceEditModel>({
  displayName: '', category: '', enable: true, extranet: false, singleton: false,
  useScope: false, address: '', healthCheck: false, remark: '',
});
const formError = ref('');

watch(
  [() => props.open, () => props.service] as const,
  ([open, svc]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(
      form,
      { displayName: '', category: '', enable: true, extranet: false, singleton: false, useScope: false, address: '', healthCheck: false, remark: '' },
      svc ?? {},
    );
  },
  { immediate: true },
);

function onSubmit(): void {
  const err = validateServiceEdit(form);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  emit('submit', { ...form });
}
</script>

<template>
  <Drawer
    :open="open"
    :title="`服务详情 · ${serviceName}`"
    :sub="service ? '编辑服务信息，或查看提供者与消费者' : ''"
    @close="emit('close')"
  >
    <div
      v-if="loading"
      class="flex items-center gap-[8px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="service-detail-loading"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      服务详情加载中…
    </div>

    <div
      v-else-if="failed || !service"
      class="py-[14px]"
      data-spec="service-detail-failed"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        服务详情加载失败，请关闭后重试
      </div>
      <Btn
        size="sm"
        class="mt-[8px]"
        data-spec="service-detail-retry"
        @click="emit('retry')"
      >
        重试
      </Btn>
    </div>

    <div
      v-else
      class="flex flex-col gap-[14px]"
      data-spec="service-detail"
    >
      <!-- 服务编辑表单 -->
      <div
        class="flex flex-col gap-[10px]"
        data-spec="service-form"
      >
        <div class="text-[12px] font-semibold text-[var(--el-text-color-primary)]">
          服务信息
        </div>
        <div class="grid grid-cols-2 gap-[10px]">
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">显示名 *</span>
            <input
              v-model="form.displayName"
              type="text"
              data-spec="service-form-displayname"
              class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">类别</span>
            <input
              v-model="form.category"
              type="text"
              data-spec="service-form-category"
              class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
            >
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">服务地址</span>
          <input
            v-model="form.address"
            type="text"
            placeholder="如 tcp://host:port（空=由注册端上报）"
            data-spec="service-form-address"
            class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
          >
        </label>
        <div class="flex flex-wrap gap-[16px]">
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.enable"
              type="checkbox"
              data-spec="service-form-enable"
              class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
            >
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">启用</span>
          </label>
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.healthCheck"
              type="checkbox"
              data-spec="service-form-healthcheck"
              class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
            >
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">健康监测</span>
          </label>
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.extranet"
              type="checkbox"
              data-spec="service-form-extranet"
              class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
            >
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">外网</span>
          </label>
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.singleton"
              type="checkbox"
              data-spec="service-form-singleton"
              class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
            >
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">单例</span>
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
          <input
            v-model="form.remark"
            type="text"
            data-spec="service-form-remark"
            class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          >
        </label>
        <p
          v-if="formError"
          class="text-[12px] text-[var(--el-color-danger)]"
          data-spec="service-form-error"
          role="alert"
        >
          {{ formError }}
        </p>
        <div class="flex justify-end gap-[8px]">
          <Btn
            variant="primary"
            :disabled="busy"
            data-spec="service-form-submit"
            @click="onSubmit"
          >
            {{ busy ? '保存中…' : '保存服务' }}
          </Btn>
        </div>
      </div>

      <!-- 提供者 -->
      <div>
        <div class="mb-[6px] flex items-center gap-[8px]">
          <span class="text-[12px] font-semibold text-[var(--el-text-color-primary)]">提供者（{{ providers.length }}）</span>
        </div>
        <table
          v-if="providers.length"
          class="w-full border-collapse overflow-hidden rounded-[8px] border border-[var(--el-border-color-light)]"
          data-spec="service-providers"
        >
          <thead>
            <tr>
              <th :class="TH">
                应用
              </th>
              <th :class="TH">
                地址
              </th>
              <th :class="[TH, 'w-[90px]']">
                健康
              </th>
              <th :class="[TH, 'w-[130px]']">
                更新时间
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="p in providers"
              :key="p.id"
              data-spec="service-provider-row"
            >
              <td :class="[TD, 'text-[12.5px] text-[var(--el-text-color-primary)]']">
                {{ p.client }}
              </td>
              <td :class="[TD, 'font-mono text-[11.5px] text-[var(--el-text-color-secondary)]']">
                {{ p.address || '—' }}
              </td>
              <td :class="TD">
                <Tag :tone="p.healthy ? 'ok' : 'warn'">
                  {{ p.healthy ? '健康' : '异常' }}
                </Tag>
                <Tag
                  v-if="p.version"
                  tone="default"
                >
                  {{ p.version }}
                </Tag>
              </td>
              <td :class="[TD, 'font-mono text-[11px] tabular-nums text-[var(--el-text-color-secondary)]']">
                {{ p.updateTimeText }}
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-else
          class="rounded-[8px] border border-[var(--el-border-color-light)] px-[12px] py-[10px] text-[12px] text-[var(--el-text-color-secondary)]"
          data-spec="service-providers-empty"
        >
          暂无提供者实例
        </div>
      </div>

      <!-- 消费者 -->
      <div>
        <div class="mb-[6px] flex items-center gap-[8px]">
          <span class="text-[12px] font-semibold text-[var(--el-text-color-primary)]">消费者（{{ consumers.length }}）</span>
        </div>
        <table
          v-if="consumers.length"
          class="w-full border-collapse overflow-hidden rounded-[8px] border border-[var(--el-border-color-light)]"
          data-spec="service-consumers"
        >
          <thead>
            <tr>
              <th :class="TH">
                应用
              </th>
              <th :class="TH">
                地址
              </th>
              <th :class="[TH, 'w-[90px]']">
                状态
              </th>
              <th :class="[TH, 'w-[130px]']">
                更新时间
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="c in consumers"
              :key="c.id"
              data-spec="service-consumer-row"
            >
              <td :class="[TD, 'text-[12.5px] text-[var(--el-text-color-primary)]']">
                {{ c.client }}
              </td>
              <td :class="[TD, 'font-mono text-[11.5px] text-[var(--el-text-color-secondary)]']">
                {{ c.address || '—' }}
              </td>
              <td :class="TD">
                <Tag :tone="c.enable ? 'ok' : 'default'">
                  {{ c.enable ? '启用' : '停用' }}
                </Tag>
              </td>
              <td :class="[TD, 'font-mono text-[11px] tabular-nums text-[var(--el-text-color-secondary)]']">
                {{ c.updateTimeText }}
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-else
          class="rounded-[8px] border border-[var(--el-border-color-light)] px-[12px] py-[10px] text-[12px] text-[var(--el-text-color-secondary)]"
          data-spec="service-consumers-empty"
        >
          暂无消费者
        </div>
      </div>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="service-detail-close"
        @click="emit('close')"
      >
        <template #icon>
          <KIcon
            name="X"
            :size="12"
          />
        </template>
        关闭
      </Btn>
    </template>
  </Drawer>
</template>
