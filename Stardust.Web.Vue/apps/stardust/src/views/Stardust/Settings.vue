<script setup lang="ts">
/**
 * 星尘控制台 · 设置（Settings）
 * 视觉基准：Doc/preview_settings.png；表单分组与存取逻辑抽 settings.model.ts。
 */
import { reactive } from 'vue';
import { ElMessage } from 'element-plus';
import SectionTitle from '@/layouts/StardustLayout/components/SectionTitle.vue';
import GlassCard from '@/layouts/StardustLayout/components/GlassCard.vue';
import { settingGroups, loadSettings, saveSettings, type SettingsState } from './settings.model';

const settings = reactive<SettingsState>(loadSettings());
const groups = settingGroups();

function save() {
  try {
    saveSettings(settings);
    ElMessage.success('设置已保存到本地');
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败');
  }
}
</script>

<template>
  <div class="flex h-full flex-col gap-5 p-6">
    <div class="flex items-center justify-between">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
          设置
        </h1>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          全局配置 · 修改后点击「保存更改」生效
        </p>
      </div>
      <button
        class="rounded-lg bg-[var(--el-color-primary)] px-4 py-2 text-sm font-medium text-white transition hover:opacity-90"
        @click="save"
      >
        保存更改
      </button>
    </div>

    <div class="grid grid-cols-2 gap-[18px]">
      <GlassCard
        v-for="g in groups"
        :key="g.key"
        class="flex flex-col gap-4 p-5"
      >
        <SectionTitle
          :title="g.title"
          size="md"
          divider
        />
        <div class="flex flex-col gap-4">
          <div
            v-for="item in g.items"
            :key="item.prop"
            class="flex items-center justify-between gap-4"
          >
            <span class="text-xs text-[var(--el-text-color-regular)]">{{ item.label }}</span>
            <div class="flex items-center">
              <el-input
                v-if="item.type === 'input'"
                v-model="(settings as any)[item.prop]"
                :placeholder="(item as any).placeholder || ''"
                size="small"
                class="sd-input w-48"
              />
              <el-switch
                v-else-if="item.type === 'switch'"
                v-model="(settings as any)[item.prop]"
                class="sd-switch"
                data-spec="toggle"
              />
              <el-slider
                v-else-if="item.type === 'slider'"
                v-model="(settings as any)[item.prop]"
                :min="0"
                :max="100"
                class="sd-slider !w-40"
              />
              <el-select
                v-else-if="item.type === 'select'"
                v-model="(settings as any)[item.prop]"
                size="small"
                class="sd-input w-48"
              >
                <el-option
                  v-for="opt in (item as any).options"
                  :key="opt"
                  :label="opt"
                  :value="opt"
                />
              </el-select>
            </div>
          </div>
        </div>
      </GlassCard>
    </div>
  </div>
</template>
