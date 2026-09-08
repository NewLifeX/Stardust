<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Platform', 'GalaxyProject');

const projects = ref<any[]>([]);
const isLoad = ref(false);

async function load() {
  const data: any = await api.getList({ pageIndex: 0, pageSize: 1000 });
  projects.value = data?.data ?? [];
}

async function onMountedAsync() {
  isLoad.value = true;
  await load();
  isLoad.value = false;
}
onMounted(onMountedAsync);

function navigateTo(row: any) {
  window.open(`/#/Platform/GalaxyProject?projectId=${row.Id}`, '_blank');
}

// ---- 新建 / 编辑项目 ----
const dialogVisible = ref(false);
const dialogFormRef = ref<any>(null);
const rules = {
  Name: [{ required: true, message: '请输入项目名称', trigger: 'blur' }],
};
const form = reactive({
  Id: 0,
  Name: '',
  Enable: true,
  IsGlobal: false,
  TenantId: 0,
  ManagerId: 0,
  Sort: 0,
  WhiteIPs: '',
  BlackIPs: '',
  NewServer: '',
  Remark: '',
});

function openModal(row?: any) {
  if (row) {
    Object.assign(form, {
      Id: row.Id ?? 0,
      Name: row.Name ?? '',
      Enable: row.Enable ?? true,
      IsGlobal: row.IsGlobal ?? false,
      TenantId: row.TenantId ?? 0,
      ManagerId: row.ManagerId ?? 0,
      Sort: row.Sort ?? 0,
      WhiteIPs: row.WhiteIPs ?? '',
      BlackIPs: row.BlackIPs ?? '',
      NewServer: row.NewServer ?? '',
      Remark: row.Remark ?? '',
    });
  } else {
    Object.assign(form, {
      Id: 0, Name: '', Enable: true, IsGlobal: false,
      TenantId: 0, ManagerId: 0, Sort: 0,
      WhiteIPs: '', BlackIPs: '', NewServer: '', Remark: '',
    });
  }
  dialogVisible.value = true;
}

async function onSubmit() {
  if (!dialogFormRef.value) return;
  await dialogFormRef.value.validate();
  const payload = JSON.parse(JSON.stringify(form));
  if (payload.Id) {
    await api.update(payload);
    ElMessage.success('更新成功');
  } else {
    await api.add(payload);
    ElMessage.success('创建成功');
  }
  dialogVisible.value = false;
  await load();
}

function closeModal() {
  dialogVisible.value = false;
}
</script>

<template>
  <div class="bg-white rounded-lg pt-4 px-4">
    <div class="flex items-center justify-between mb-4">
      <div class="font-bold text-base">
        项目列表
      </div>
      <div class="flex gap-2">
        <el-button
          type="success"
          @click="openModal()"
        >
          <i class="el-icon-plus" />
          新建项目
        </el-button>
        <el-button
          type="primary"
          @click="load"
        >
          <i class="el-icon-refresh" />
          刷新
        </el-button>
      </div>
    </div>

    <el-table
      v-loading="isLoad"
      :data="projects"
      border
      stripe
    >
      <el-table-column
        label="名称"
        min-width="160"
      >
        <template #default="{ row }">
          <el-button
            link
            type="primary"
            @click="openModal(row)"
          >
            {{ row.Name }}
          </el-button>
        </template>
      </el-table-column>
      <el-table-column
        prop="Enable"
        label="启用"
        width="80"
        align="center"
      >
        <template #default="{ row }">
          <el-tag :type="row.Enable ? 'success' : 'info'">
            {{ row.Enable ? '是' : '否' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        prop="Nodes"
        label="节点数"
        width="90"
        align="center"
      />
      <el-table-column
        prop="Apps"
        label="应用数"
        width="90"
        align="center"
      />
      <el-table-column
        prop="Sort"
        label="排序"
        width="80"
        align="center"
      />
      <el-table-column
        label="操作"
        width="140"
        fixed="right"
      >
        <template #default="{ row }">
          <el-button
            link
            type="primary"
            @click="navigateTo(row)"
          >
            进入
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog
      v-model="dialogVisible"
      :title="form.Id ? '编辑项目' : '新建项目'"
      width="560px"
    >
      <el-form
        ref="dialogFormRef"
        :model="form"
        :rules="rules"
        label-width="100px"
      >
        <el-form-item
          label="名称"
          prop="Name"
        >
          <el-input
            v-model="form.Name"
            placeholder="项目名称（唯一）"
          />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.Enable" />
        </el-form-item>
        <el-form-item label="全局">
          <el-switch v-model="form.IsGlobal" />
        </el-form-item>
        <el-form-item label="租户ID">
          <el-input-number
            v-model="form.TenantId"
            :min="0"
          />
        </el-form-item>
        <el-form-item label="管理者ID">
          <el-input-number
            v-model="form.ManagerId"
            :min="0"
          />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number
            v-model="form.Sort"
            :min="0"
          />
        </el-form-item>
        <el-form-item label="IP白名单">
          <el-input
            v-model="form.WhiteIPs"
            placeholder="支持*通配符，多个逗号隔开"
          />
        </el-form-item>
        <el-form-item label="IP黑名单">
          <el-input
            v-model="form.BlackIPs"
            placeholder="支持*通配符，多个逗号隔开"
          />
        </el-form-item>
        <el-form-item label="新服务器">
          <el-input
            v-model="form.NewServer"
            placeholder="该项目下节点自动迁移到的服务器地址"
          />
        </el-form-item>
        <el-form-item label="备注">
          <el-input
            v-model="form.Remark"
            type="textarea"
            :rows="2"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="closeModal">
          取消
        </el-button>
        <el-button
          type="primary"
          @click="onSubmit"
        >
          保存
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style lang="scss" scoped>
</style>
