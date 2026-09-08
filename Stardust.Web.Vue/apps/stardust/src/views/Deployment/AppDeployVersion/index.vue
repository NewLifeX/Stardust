<script setup lang="ts">
/**
 * Deployment/AppDeployVersion/Index — 应用版本列表（自定义迁移页）
 *
 * MVC 源 _Form_File.cshtml + 控制器 ListFields 还原：
 *  - 列表列：编号 Id / 发布集 DeployName（**可点击链接** -> #/Deployment/AppDeploy?deployId=<DeployId>）
 *            / 版本 Version / 大小 Size / 哈希 Hash / 启用 Enable / 文件 Url（列表显示「下载」链接）/ 创建时间 等
 *  - 隐藏页兜底：[Menu(80,false)] 隐藏页，框架不会注册路由，靠 DefaultEntity 视图兜底渲染本自定义页
 *  - 「使用版本」操作列：每行一个按钮 -> confirm -> GET /Deployment/AppDeployVersion/UseVersion?Id=<行Id>
 *  - 文件上传新增：新增版本需上传 zip 包，用 el-upload POST 到 Save（字段名 Url_attachment，accept=zip）
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Deployment/AppDeployVersion/UseVersion
 * （request 会自动拼 baseURL，不要写 /api/... 否则双写 404）。
 */
import { ref, computed, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Deployment', 'AppDeployVersion');

interface Row {
  Id?: number;
  DeployId?: number;
  DeployName?: string;
  Version?: string;
  Size?: number;
  Hash?: string;
  Enable?: boolean;
  Url?: string;
  TraceId?: string;
  [k: string]: unknown;
}

/** 标准列（Url 需自建「下载」链接，不交给 AutoPage 渲染）
 * 列集合/顺序严格对应 MVC 控制器 ListFields：
 *  RemoveField(Hash,CommitId,CommitLog) / RemoveCreateField / RemoveRemarkField / TraceUrl
 *  AddListField("UseVersion", null, "Enable") 插入在 启用 之后
 * 列名以实体 DisplayName 为权威。 */
type Col = { name: string; label: string; width?: number };
const ALL_COLUMNS: Col[] = [
  { name: 'Id', label: '编号', width: 80 },
  { name: 'DeployName', label: '应用部署集' },
  { name: 'Version', label: '版本', width: 150 },
  { name: 'Enable', label: '启用', width: 80 },
  { name: 'Url', label: '资源地址', width: 120 },
  { name: 'Overwrite', label: '覆盖文件', width: 90 },
  { name: 'Mode', label: '发布模式', width: 90 },
  { name: 'Size', label: '文件大小', width: 110 },
  { name: 'OS', label: '操作系统', width: 100 },
  { name: 'Arch', label: 'CPU架构', width: 90 },
  { name: 'TargetFramework', label: '目标框架', width: 120 },
  { name: 'Progress', label: '进度', width: 90 },
  { name: 'CommitTime', label: '提交时间', width: 160 },
  { name: 'TraceId', label: '追踪', width: 120 },
];

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

/** 搜索：deployId / dtStart / dtEnd / Q（高级搜索，来自控制器 Search()） */
const deployId = ref<number>(0);
const q = ref('');
const timeRange = ref<[string, string] | null>(null);

const hasActiveFilter = computed(
  () => deployId.value > 0 || q.value !== '' || timeRange.value != null,
);

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Enable') return v ? '是' : '否';
  if (key === 'Size') return formatSize(Number(v));
  if (key === 'CreateTime' || key === 'CommitTime') return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}

function formatSize(bytes: number): string {
  if (!bytes || bytes <= 0) return '-';
  const units = ['B', 'KB', 'MB', 'GB'];
  let i = 0;
  let n = bytes;
  while (n >= 1024 && i < units.length - 1) {
    n /= 1024;
    i++;
  }
  return `${n.toFixed(i === 0 ? 0 : 2)} ${units[i]}`;
}

function deployNameUrl(row: Row): string {
  return `#/Deployment/AppDeploy?deployId=${row.DeployId ?? row.Id}`;
}

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (deployId.value > 0) p.deployId = deployId.value;
  if (q.value) p.Q = q.value;
  if (timeRange.value?.[0]) p.dtStart = timeRange.value[0];
  if (timeRange.value?.[1]) p.dtEnd = timeRange.value[1];
  return p;
}

async function fetchList() {
  loading.value = true;
  try {
    const res: any = await api.getList<Row>(buildParams());
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppDeployVersion] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}

function handleReset() {
  deployId.value = 0;
  q.value = '';
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

/** 「使用版本」：GET /Deployment/AppDeployVersion/UseVersion?Id=<行Id> */
async function onUseVersion(row: Row) {
  if (row.Id == null) return;
  try {
    await ElMessageBox.confirm('确认将该版本应用到应用部署集？', '使用版本', {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    });
  } catch {
    return; // 用户取消
  }
  try {
    const res: any = await request({
      url: `/Deployment/AppDeployVersion/UseVersion`,
      method: 'get',
      params: { Id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '操作成功');
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    await fetchList();
  }
}

/* ---------- 新增版本（el-upload 上传 zip） ---------- */
const addVisible = ref(false);
const addLoading = ref(false);
const addDeployId = ref<number | undefined>(undefined);
const uploadFile = ref<File | null>(null);
const uploadRef = ref();

function openAdd() {
  addVisible.value = true;
  addDeployId.value = undefined;
  uploadFile.value = null;
}

function onUploadChange(file: any) {
  uploadFile.value = file?.raw ?? file;
}

function onUploadRemove() {
  uploadFile.value = null;
}

async function onSubmitAdd() {
  if (addDeployId.value == null || addDeployId.value <= 0) {
    ElMessage.warning('请填写发布集编号');
    return;
  }
  if (!uploadFile.value) {
    ElMessage.warning('请选择 zip 包');
    return;
  }
  addLoading.value = true;
  try {
    const form = new FormData();
    form.append('DeployId', String(addDeployId.value));
    form.append('Url_attachment', uploadFile.value as Blob);
    const res: any = await request({
      url: `/Deployment/AppDeployVersion/Save`,
      method: 'post',
      data: form,
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '新增成功');
      addVisible.value = false;
      await fetchList();
    } else {
      ElMessage.error(res?.message || '新增失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '新增失败');
  } finally {
    addLoading.value = false;
  }
}

onMounted(async () => {
  // 从 hash 读取 deployId（来自 AppDeploy 的「版本」跳转）
  const m = location.hash.match(/[?&]deployId=(\d+)/);
  if (m) deployId.value = Number(m[1]);
  await fetchList();
});
</script>

<template>
  <div class="list-page app-deploy-version-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用版本
      </h2>
      <div class="lp-toolbar">
        <el-button
          type="primary"
          @click="openAdd"
        >
          新增版本
        </el-button>
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
      </div>
    </div>

    <div class="lp-card">
      <!-- 搜索栏：发布集编号 / 关键字 / 时间范围 -->
      <div class="lp-card-header adv-search">
        <div class="adv-search-grid">
          <div class="adv-search-field">
            <label class="adv-search-label">发布集编号</label>
            <el-input
              v-model.number="deployId"
              type="number"
              placeholder="DeployId"
              class="adv-search-input"
              clearable
            />
          </div>

          <div class="adv-search-field">
            <label class="adv-search-label">关键字</label>
            <el-input
              v-model="q"
              placeholder="Q"
              class="adv-search-input"
              clearable
            />
          </div>

          <div class="adv-search-field adv-search-field--wide">
            <label class="adv-search-label">时间</label>
            <el-date-picker
              v-model="timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="adv-search-input adv-search-datepicker"
            />
          </div>

          <div class="adv-search-actions">
            <el-button
              :class="{ 'is-active': hasActiveFilter }"
              @click="handleReset"
            >
              重置
            </el-button>
            <el-button
              type="primary"
              @click="handleSearch"
            >
              <el-icon><Search /></el-icon>搜索
            </el-button>
          </div>
        </div>
      </div>

      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="act-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="c in ALL_COLUMNS"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :width="c.width"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- 发布集：可点击链接跳 AppDeploy -->
              <el-link
                v-if="c.name === 'DeployName'"
                type="primary"
                :underline="false"
                :href="deployNameUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) || '(空)' }}
              </el-link>
              <!-- 文件：下载链接 -->
              <el-link
                v-else-if="c.name === 'Url'"
                type="primary"
                :underline="false"
                :href="row.Url"
                target="_blank"
                :disabled="!row.Url"
              >
                {{ row.Url ? '下载' : '-' }}
              </el-link>
              <span v-else>{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>

          <!-- 使用版本操作列 -->
          <el-table-column
            label="操作"
            width="120"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <el-button
                size="small"
                type="primary"
                @click="onUseVersion(row)"
              >
                使用版本
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <div class="lp-card-footer">
        <el-pagination
          :current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next, sizes"
          :page-sizes="[10, 20, 50, 100]"
          @current-change="onPageChange"
          @size-change="
            (s: number) => {
              pageSize = s;
              page = 1;
              fetchList();
            }
          "
        />
      </div>
    </div>

    <!-- 新增版本弹窗 -->
    <el-dialog
      v-model="addVisible"
      title="新增版本"
      width="520px"
      :close-on-click-modal="false"
    >
      <el-form label-width="100px">
        <el-form-item
          label="发布集编号"
          required
        >
          <el-input
            v-model.number="addDeployId"
            type="number"
            placeholder="请输入 DeployId"
          />
        </el-form-item>
        <el-form-item
          label="zip 包"
          required
        >
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            :on-change="onUploadChange"
            :on-remove="onUploadRemove"
            accept="application/zip"
            drag
          >
            <el-icon class="el-icon--upload">
              <UploadFilled />
            </el-icon>
            <div class="el-upload__text">
              将 zip 文件拖到此处，或<em>点击上传</em>
            </div>
            <template #tip>
              <div class="el-upload__tip">
                仅支持 application/zip 格式
              </div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="addVisible = false">
          取消
        </el-button>
        <el-button
          type="primary"
          :loading="addLoading"
          @click="onSubmitAdd"
        >
          提交
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style lang="scss" scoped>
.app-deploy-version-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .lp-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .lph-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .lp-toolbar {
      display: flex;
      gap: 8px;
    }
  }

  .lp-card {
    flex: 1;
    margin: var(--el-main-padding, 24px);
    padding: 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .lp-card-header {
    padding: 16px 20px;
    border-bottom: 1px solid var(--el-border-color-light);
  }

  .lp-card-body {
    flex: 1;
    min-height: 200px;
    overflow: auto;
  }

  .lp-card-footer {
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.adv-search {
  .adv-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .adv-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .adv-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .adv-search-input {
    width: 100%;

    :deep(.el-input__wrapper),
    :deep(.el-select__wrapper),
    :deep(.el-range-editor) {
      min-height: 36px;
      border-radius: var(--el-border-radius-base);
      background: var(--el-bg-color);
      border: 1px solid var(--el-border-color-light);
      box-shadow: none;
      transition:
        border-color 0.15s ease,
        background 0.15s ease,
        box-shadow 0.15s ease;
    }

    :deep(.el-input__wrapper.is-focus),
    :deep(.el-select__wrapper.is-focused),
    :deep(.el-range-editor.is-active) {
      background: var(--el-fill-color-blank);
      border-color: var(--el-color-primary);
      box-shadow:
        0 0 0 1px var(--el-color-primary) inset,
        0 0 0 3px var(--el-color-primary-light-8);
    }

    :deep(.el-input__inner),
    :deep(.el-select__selected-item) {
      font-size: 13px;
      color: var(--el-text-color-primary);
    }
  }

  .adv-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .adv-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

@media (max-width: 900px) {
  .adv-search .adv-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .adv-search .adv-search-grid {
    grid-template-columns: 1fr;
  }

  .adv-search .adv-search-field--wide {
    grid-column: span 1;
  }
}
</style>
