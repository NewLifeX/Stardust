<script setup lang="ts">
/**
 * 设置（Settings）—— 按设计交接包 PageSettings.jsx 重构
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/PageSettings.jsx
 *   形态：星座画布（6 个设置域环绕「全局设置」中心，高 520）+ 下方展开的设置面板
 *   面板：标题 + 描述 + 「恢复默认 / 保存修改」；双列 field 网格；每行下边框
 *   控件：toggle 36×20 胶囊 / radio 分段切换器 / input 深底等宽 + 单位后缀
 *   面包屑：星辰 › 概览 › 设置域
 *
 * 逻辑层：settings.model.ts（6 域 schema + 本地持久化），本文件只负责呈现。
 * 样式纪律：颜色零硬编码，全部取设计令牌；图标为 Lucide 线性 path 内联 SVG。
 */
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import GlassCard from '@/layouts/StardustLayout/components/GlassCard.vue'
import CoreConstellation from '@/layouts/StardustLayout/components/CoreConstellation.vue'
import {
  DOMAINS,
  domainByKey,
  isMonoOption,
  loadSettings,
  resetDomain,
  saveSettings,
  type SettingField,
  type SettingsState,
} from './settings.model'

const router = useRouter()

/** 当前选中的设置域（设计稿默认 registry） */
const sel = ref<string>(DOMAINS[0].key)
const settings = reactive<SettingsState>(loadSettings())

const domain = computed(() => domainByKey(sel.value) ?? DOMAINS[0])

/** 星座节点：6 个设置域（icon 为 Lucide path，path = 域 key） */
const nodes = computed(() =>
  DOMAINS.map((d) => ({ key: d.key, label: d.label, sub: d.hint, icon: d.icon, path: d.key })),
)

function val(f: SettingField): string | number | boolean {
  return settings[sel.value]?.[f.key] ?? f.def
}

function setVal(f: SettingField, v: string | number | boolean): void {
  if (!settings[sel.value]) settings[sel.value] = {}
  settings[sel.value][f.key] = v
}

function flip(f: SettingField): void {
  setVal(f, !val(f))
}

function onInput(f: SettingField, e: Event): void {
  setVal(f, (e.target as HTMLInputElement).value)
}

function pickDomain(key: string): void {
  sel.value = key
}

function save(): void {
  try {
    saveSettings(settings)
    ElMessage.success('设置已保存')
  } catch (e) {
    ElMessage.error((e as Error)?.message || '保存失败')
  }
}

function restore(): void {
  resetDomain(settings, sel.value)
  ElMessage.info(`「${domain.value.title}」已恢复默认`)
}

function goOverview(): void {
  void router.push('/stardust/overview')
}
</script>

<template>
  <div
    class="sd-settings sd-page-stack"
    data-spec="settings-page"
  >
    <!-- ── 面包屑 ─────────────────────────────────────────────────────────── -->
    <nav
      class="sd-crumb"
      data-spec="settings-crumb"
    >
      <button
        type="button"
        class="sd-crumb__link"
        @click="goOverview"
      >
        星辰
      </button>
      <span class="sd-crumb__sep">›</span>
      <button
        type="button"
        class="sd-crumb__link"
        @click="goOverview"
      >
        概览
      </button>
      <span class="sd-crumb__sep">›</span>
      <span class="sd-crumb__cur">设置域</span>
    </nav>

    <!-- ── 设置域星座（高 520，设计稿 height=520） ────────────────────────── -->
    <GlassCard
      class="sd-const-wrap"
      data-spec="settings-constellation"
    >
      <CoreConstellation
        :nodes="nodes"
        :center="{ label: '全局设置', sub: 'PLATFORM SETTINGS' }"
        @select="pickDomain"
      />
    </GlassCard>

    <!-- ── 设置面板 ───────────────────────────────────────────────────────── -->
    <section
      class="sd-panel"
      data-spec="settings-panel"
    >
      <header class="sd-panel__head">
        <div class="sd-panel__titles">
          <div
            class="sd-panel__title"
            data-spec="settings-panel-title"
          >
            {{ domain.title }}
          </div>
          <div
            class="sd-panel__desc"
            data-spec="settings-panel-desc"
          >
            {{ domain.desc }}
          </div>
        </div>
        <span class="sd-spacer" />
        <button
          type="button"
          class="sd-btn sd-btn--ghost"
          data-spec="settings-reset"
          @click="restore"
        >
          恢复默认
        </button>
        <button
          type="button"
          class="sd-btn sd-btn--primary"
          data-spec="settings-save"
          @click="save"
        >
          <svg
            class="sd-ico"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <path d="M20 6L9 17l-5-5" />
          </svg>
          保存修改
        </button>
      </header>

      <div
        class="sd-fields"
        data-spec="settings-fields"
      >
        <div
          v-for="f in domain.fields"
          :key="f.key"
          class="sd-field"
          data-spec="settings-field"
        >
          <div
            class="sd-field__label"
            data-spec="settings-field-label"
          >
            {{ f.label }}
          </div>
          <div class="sd-field__ctl">
            <!-- toggle 36×20 -->
            <button
              v-if="f.kind === 'toggle'"
              type="button"
              role="switch"
              class="sd-toggle"
              :class="{ 'is-on': val(f) === true }"
              :aria-checked="val(f) === true"
              :title="f.label"
              data-spec="settings-toggle"
              @click="flip(f)"
            >
              <span
                class="sd-toggle__knob"
                :class="{ 'is-on': val(f) === true }"
              />
            </button>

            <!-- radio 分段切换器 -->
            <div
              v-else-if="f.kind === 'radio'"
              class="sd-radio"
              data-spec="settings-radio"
            >
              <button
                v-for="o in f.options"
                :key="o"
                type="button"
                class="sd-radio__btn"
                :class="{ 'is-active': val(f) === o, 'is-mono': isMonoOption(o) }"
                data-spec="settings-radio-btn"
                @click="setVal(f, o)"
              >
                {{ o }}
              </button>
            </div>

            <!-- input 深底等宽 + 单位后缀 -->
            <div
              v-else
              class="sd-input"
              data-spec="settings-input"
            >
              <input
                type="text"
                :value="val(f)"
                :aria-label="f.label"
                @input="onInput(f, $event)"
              >
              <span
                v-if="f.suffix"
                class="sd-input__suffix"
              >{{ f.suffix }}</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.sd-settings {
  min-width: 0;
}

/* ---- 面包屑 ---- */
.sd-crumb {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--fg-3);
}
.sd-crumb__link {
  padding: 0;
  border: none;
  background: none;
  font-family: inherit;
  font-size: 12px;
  color: var(--fg-3);
  cursor: pointer;
}
.sd-crumb__link:hover {
  color: var(--brand-300);
}
.sd-crumb__sep {
  color: var(--fg-4);
}
.sd-crumb__cur {
  color: var(--fg-1);
  font-weight: 500;
}

/* ---- 星座画布：设计稿 height 520 ---- */
.sd-const-wrap {
  height: 520px;
  padding: 0;
  overflow: hidden;
}

/* ---- 设置面板 ---- */
.sd-panel {
  padding: 20px 22px;
  border-radius: var(--radius-xl);
  background: var(--surface-1);
  border: 1px solid var(--border-1);
}
.sd-panel__head {
  display: flex;
  align-items: flex-end;
  gap: 10px;
  margin-bottom: 18px;
}
.sd-panel__titles {
  min-width: 0;
}
.sd-panel__title {
  font-size: 16px;
  font-weight: 600;
  color: var(--fg-1);
  letter-spacing: -0.01em;
}
.sd-panel__desc {
  margin-top: 3px;
  font-size: 12.5px;
  color: var(--fg-3);
}
.sd-spacer {
  flex: 1;
}

/* ---- 按钮 ---- */
.sd-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-2);
  background: var(--surface-2);
  color: var(--fg-1);
  font-family: var(--font-sans);
  font-size: 12.5px;
  font-weight: 500;
  line-height: 1;
  white-space: nowrap;
  cursor: pointer;
  transition: border-color var(--dur-micro) var(--ease-out), background var(--dur-micro) var(--ease-out);
}
.sd-btn:hover {
  border-color: var(--brand-500);
}
.sd-btn--ghost {
  background: transparent;
  border-color: var(--border-1);
  color: var(--fg-2);
}
.sd-btn--primary {
  background: var(--sd-gradient-btn);
  border-color: transparent;
  box-shadow: var(--sd-glow-btn);
}
.sd-btn--primary:hover {
  filter: brightness(1.08);
}
.sd-ico {
  width: 13px;
  height: 13px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.5;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* ---- 字段网格：双列，gap 14 / 32（设计稿） ---- */
.sd-fields {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 14px 32px;
}
@media (max-width: 1100px) {
  .sd-fields {
    grid-template-columns: 1fr;
  }
}
.sd-field {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 16px;
  align-items: center;
  padding: 12px 0;
  border-bottom: 1px solid var(--border-1);
  min-width: 0;
}
.sd-field__label {
  font-size: 13.5px;
  color: var(--fg-1);
  min-width: 0;
}
.sd-field__ctl {
  display: flex;
  align-items: center;
}

/* ---- toggle 36×20 胶囊 ---- */
.sd-toggle {
  position: relative;
  width: 36px;
  height: 20px;
  padding: 0;
  border: none;
  border-radius: var(--radius-pill);
  background: var(--surface-3);
  cursor: pointer;
  transition: background var(--dur-short) var(--ease-out);
}
.sd-toggle.is-on {
  background: var(--brand-500);
}
.sd-toggle__knob {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 14px;
  height: 14px;
  border-radius: var(--radius-pill);
  background: var(--fg-1);
  box-shadow: 0 1px 3px color-mix(in srgb, var(--bg) 65%, transparent);
  transition: left var(--dur-short) var(--ease-out);
}
.sd-toggle__knob.is-on {
  left: 19px;
}

/* ---- radio 分段切换器 ---- */
.sd-radio {
  display: flex;
  gap: 6px;
  padding: 3px;
  border-radius: var(--radius-md);
  background: var(--surface-2);
  border: 1px solid var(--border-1);
}
.sd-radio__btn {
  padding: 5px 12px;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--fg-2);
  font-family: var(--font-sans);
  font-size: 12px;
  font-weight: 500;
  line-height: 1.2;
  white-space: nowrap;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.sd-radio__btn:hover {
  color: var(--fg-1);
}
.sd-radio__btn.is-active {
  background: var(--brand-500);
  color: var(--fg-1);
}
.sd-radio__btn.is-mono {
  font-family: var(--font-mono);
}

/* ---- input 深底等宽 ---- */
.sd-input {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 130px;
  padding: 5px 10px;
  border-radius: var(--radius-sm);
  background: var(--surface-2);
  border: 1px solid var(--border-1);
}
.sd-input input {
  flex: 1;
  width: 100%;
  min-width: 0;
  border: none;
  outline: none;
  background: transparent;
  color: var(--fg-1);
  font-family: var(--font-mono);
  font-size: 13px;
}
.sd-input:focus-within {
  border-color: color-mix(in srgb, var(--brand-500) 60%, transparent);
}
.sd-input__suffix {
  flex-shrink: 0;
  font-size: 11px;
  color: var(--fg-3);
  white-space: nowrap;
}
</style>
