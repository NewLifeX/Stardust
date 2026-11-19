<script setup lang="ts">
/**
 * StarServerSettingsForm（CARD-PLT-07）：平台设置完整表单抽屉。
 * 数据源 GET /Platform/StarServer（fetchMcpRuntime 同源）；保存 PUT **全量覆盖**
 * （ConfigController.Update = cfg.Copy(value)+Save，请求体缺省字段回写类型默认值），
 * 故 patch 仅含改动字段、GET 到的 raw 原样回传（saveStarServerSettings）。
 * 密钥字段（TokenSecret/TraceShareSecret）mask：不回显原值，留空 = 不进 patch = 保持不变；
 * 轮换后果在保存确认（GuardModal）文案中披露。只读字段（McpActionSet/SensitiveKeywords，S4 定稿）纯文本展示。
 * 字段清单唯一真源 = STAR_SERVER_FORM_FIELDS（Doc/用户旅程/平台/StarServer.md §10 附录）。
 */
import { computed, ref, watch } from 'vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import { feedbackRemainMs } from './galaxyProject.logic.ts';
import { fetchMcpRuntime, saveStarServerSettings, STAR_SERVER_FORM_FIELDS, type StarServerFormField } from './mcpToken.logic.ts';
import { toast } from './useToast';

const props = defineProps<{ req: Req; open: boolean }>();
const emit = defineEmits<{ (e: 'close'): void; (e: 'saved'): void }>();

const loading = ref(false);
const error = ref('');
const raw = ref<Record<string, unknown> | null>(null);
const draft = ref<Record<string, number | boolean | string>>({});
const saving = ref(false);
const guardOpen = ref(false);
const saveError = ref('');

const camel = (name: string): string => name.charAt(0).toLowerCase() + name.slice(1);

/** raw 键 PascalCase/camelCase 双兼容读取（后端反序列化绑定两种都可能出现） */
function read(name: string): unknown {
  const r = raw.value;
  if (!r) return undefined;
  if (name in r) return r[name];
  const c = camel(name);
  if (c in r) return r[c];
  return undefined;
}

function initDraft(): void {
  const d: Record<string, number | boolean | string> = {};
  for (const f of STAR_SERVER_FORM_FIELDS) {
    const v = read(f.name);
    if (f.type === 'number' || f.type === 'enum') d[f.name] = typeof v === 'number' ? v : Number(v ?? 0) || 0;
    else if (f.type === 'boolean') d[f.name] = v === true || v === 'true';
    else if (f.type === 'secret') d[f.name] = ''; // 不回显密钥；留空 = 保持不变
    else d[f.name] = v == null ? '' : String(v);
  }
  draft.value = d;
}

async function load(): Promise<void> {
  if (loading.value) return;
  loading.value = true;
  error.value = '';
  raw.value = null;
  try {
    const r = await fetchMcpRuntime(props.req);
    if (r.raw) {
      raw.value = r.raw as Record<string, unknown>;
      initDraft();
    } else {
      error.value = r.error || '取不到当前设置';
    }
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.open,
  (open) => {
    if (open) void load();
  },
  { immediate: true },
);

/** 组标题按数组首现顺序（密钥/只读殿后），渲染序 = STAR_SERVER_FORM_FIELDS 数组序 */
const groups = computed(() => {
  const titles: string[] = [];
  for (const f of STAR_SERVER_FORM_FIELDS) if (!titles.includes(f.group)) titles.push(f.group);
  return titles.map((title) => ({ title, fields: STAR_SERVER_FORM_FIELDS.filter((f) => f.group === title) }));
});

/** 改动判定：密钥以「有输入」为改（原值本就不回显）；其余与 raw 原值比较 */
function changed(f: StarServerFormField): boolean {
  if (f.type === 'secret') return String(draft.value[f.name] ?? '').trim() !== '';
  const v = draft.value[f.name];
  const o = read(f.name);
  if (f.type === 'number' || f.type === 'enum') {
    return (typeof v === 'number' ? v : Number(v) || 0) !== (typeof o === 'number' ? o : Number(o ?? 0) || 0);
  }
  if (f.type === 'boolean') return v === true !== (o === true || o === 'true');
  return String(v ?? '') !== (o == null ? '' : String(o));
}

const patch = computed<Record<string, unknown>>(() => {
  const p: Record<string, unknown> = {};
  for (const f of STAR_SERVER_FORM_FIELDS) {
    if (f.readonly || !changed(f)) continue;
    const v = draft.value[f.name];
    if (f.type === 'number' || f.type === 'enum') p[f.name] = typeof v === 'number' ? v : Number(v) || 0;
    else if (f.type === 'boolean') p[f.name] = v === true;
    else p[f.name] = String(v ?? '').trim();
  }
  return p;
});

const patchCount = computed(() => Object.keys(patch.value).length);
const noChange = computed(() => patchCount.value === 0);

const inputCls =
  'w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[5px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)] disabled:cursor-not-allowed disabled:opacity-60';

function setStr(f: StarServerFormField, e: Event): void {
  draft.value[f.name] = (e.target as HTMLInputElement).value;
}
function setNum(f: StarServerFormField, e: Event): void {
  draft.value[f.name] = Number((e.target as HTMLInputElement).value) || 0;
}
function setBool(f: StarServerFormField, e: Event): void {
  draft.value[f.name] = (e.target as HTMLInputElement).checked;
}
function setEnum(f: StarServerFormField, e: Event): void {
  draft.value[f.name] = Number((e.target as HTMLSelectElement).value);
}

async function confirmSave(): Promise<void> {
  if (noChange.value || saving.value) return;
  guardOpen.value = false;
  saving.value = true;
  saveError.value = '';
  const started = Date.now();
  try {
    const r = await saveStarServerSettings(props.req, raw.value, patch.value);
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await new Promise<void>((resolve) => { setTimeout(resolve, rest); });
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      emit('close');
      emit('saved');
    } else {
      saveError.value = r.message;
    }
  } finally {
    saving.value = false;
  }
}

const guardLabel = computed(() => {
  const lines = [
    `保存将把当前表单完整回存到平台设置（共 ${patchCount.value} 项改动）。平台设置保存为全量覆盖，未在本表单出现的内容不会被改动。`,
    '密钥留空 = 保持不变；填写新值 = 立即轮换：令牌密钥轮换会使全量令牌失效，调用链分享密钥轮换会使全部分享链接失效。',
  ];
  return lines.join('\n');
});
</script>

<template>
  <Drawer
    :open="open"
    title="平台设置"
    sub="完整平台设置（StarServer），保存为全量覆盖；密钥留空表示保持不变"
    @close="emit('close')"
  >
    <div data-spec="star-server-form">
      <div
        v-if="loading"
        data-spec="star-server-form-loading"
        class="flex items-center gap-[8px] px-[4px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        平台设置加载中…
      </div>

      <div
        v-else-if="error"
        data-spec="star-server-form-error"
        class="px-[4px] py-[14px] text-[12.5px] text-[var(--el-color-danger)]"
      >
        平台设置加载失败：{{ error }}
      </div>

      <div
        v-else
        class="flex flex-col gap-[16px]"
      >
        <div
          v-for="g in groups"
          :key="g.title"
          :data-spec="`star-server-form-group-${g.title}`"
        >
          <div class="mb-[8px] border-b border-[var(--el-border-color-light)] pb-[4px] text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
            {{ g.title }}
          </div>
          <div class="grid grid-cols-1 gap-[10px] md:grid-cols-2">
            <label
              v-for="f in g.fields"
              :key="f.name"
              class="block"
            >
              <div class="mb-[4px] flex items-baseline gap-[6px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
                {{ f.label }}
                <span class="text-[11px] font-normal text-[var(--el-text-color-secondary)]">{{ f.hint }}</span>
              </div>

              <!-- 只读：纯文本展示（S4 定稿语义，禁改） -->
              <div
                v-if="f.readonly"
                :data-spec="`star-server-form-field-${f.name}`"
                class="rounded-[8px] border border-dashed border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[5px] text-[12.5px] text-[var(--el-text-color-secondary)]"
              >
                {{ String(read(f.name) ?? '') }}
              </div>

              <!-- 密钥：不回显，留空 = 保持不变 -->
              <input
                v-else-if="f.type === 'secret'"
                :data-spec="`star-server-form-field-${f.name}`"
                type="text"
                autocomplete="new-password"
                placeholder="已配置（留空保持不变）"
                :value="String(draft[f.name] ?? '')"
                :disabled="saving"
                :class="inputCls"
                @input="setStr(f, $event)"
              >

              <!-- 枚举：数值选项（与后端枚举值逐字一致） -->
              <select
                v-else-if="f.type === 'enum'"
                :data-spec="`star-server-form-field-${f.name}`"
                :value="(draft[f.name] ?? 0)"
                :disabled="saving"
                :class="[inputCls, 'cursor-pointer']"
                @change="setEnum(f, $event)"
              >
                <option
                  v-for="opt in f.options"
                  :key="opt.value"
                  :value="opt.value"
                >
                  {{ opt.label }}（{{ opt.value }}）
                </option>
              </select>

              <!-- 布尔 -->
              <span
                v-else-if="f.type === 'boolean'"
                class="flex items-center gap-[8px] py-[5px]"
              >
                <input
                  :data-spec="`star-server-form-field-${f.name}`"
                  type="checkbox"
                  :checked="draft[f.name] === true"
                  :disabled="saving"
                  class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
                  @change="setBool(f, $event)"
                >
                <span class="text-[12px] text-[var(--el-text-color-secondary)]">{{ draft[f.name] === true ? '已启用' : '已关闭' }}</span>
              </span>

              <!-- 逗号分隔列表 -->
              <textarea
                v-else-if="f.type === 'csv'"
                :data-spec="`star-server-form-field-${f.name}`"
                rows="2"
                :value="String(draft[f.name] ?? '')"
                :disabled="saving"
                :class="inputCls"
                @input="setStr(f, $event)"
              />

              <!-- 数值 -->
              <input
                v-else-if="f.type === 'number'"
                :data-spec="`star-server-form-field-${f.name}`"
                type="number"
                :value="Number(draft[f.name] ?? 0)"
                :disabled="saving"
                :class="inputCls"
                @input="setNum(f, $event)"
              >

              <!-- 普通文本 -->
              <input
                v-else
                :data-spec="`star-server-form-field-${f.name}`"
                type="text"
                :value="String(draft[f.name] ?? '')"
                :disabled="saving"
                :class="inputCls"
                @input="setStr(f, $event)"
              >
            </label>
          </div>
        </div>

        <div
          v-if="saveError"
          data-spec="star-server-form-save-error"
          class="rounded-[8px] border border-[var(--el-color-danger)] px-[10px] py-[8px] text-[12px] text-[var(--el-color-danger)]"
        >
          {{ saveError }}
        </div>
        <div
          v-else-if="noChange"
          data-spec="star-server-form-no-change"
          class="rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[8px] text-[12px] text-[var(--el-text-color-regular)]"
        >
          暂无改动。修改任意字段后可保存。
        </div>
      </div>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="star-server-form-cancel"
        :disabled="saving"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="star-server-form-save"
        :disabled="noChange || saving"
        :aria-busy="saving"
        @click="guardOpen = true"
      >
        <template
          v-if="saving"
          #icon
        >
          <span class="inline-flex animate-spin">
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        {{ saving ? '保存中…' : `保存（${patchCount} 项改动）` }}
      </Btn>
    </template>
  </Drawer>

  <GuardModal
    v-if="guardOpen"
    label="保存平台设置（全量覆盖）"
    :detail="guardLabel"
    @cancel="guardOpen = false"
    @confirm="confirmSave"
  />
</template>
