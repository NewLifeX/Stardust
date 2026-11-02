<script setup lang="ts">
/**
 * 星尘控制台 · 登录页 Login — A4
 * ---------------------------------------------------------------------------
 * 设计真源（权威）：design-handoff/ui_kits/console/LoginPage.jsx
 *                  design-handoff/design2/README.md §1 Login
 * 基线截图：design-handoff/design2/_verify/*.png
 *
 * 结构（与设计稿 1:1 对齐）：
 *   全屏星云背景（复用 StarfieldBg，intensity=1）
 *   └ 居中 1080×660 毛玻璃卡片，左右两栏（1.1fr / 0.9fr）
 *     ├ 左栏：顶部 logo + 版本徽章 → 中间轨道核心 220×220（三层同心圆 r=105/82/56
 *     │        + 3 发光轨道点 + 中心 96 圆 + logo-mark）→ 底部 ticker（4 段 / 3200ms）
 *     └ 右栏：标题 → SSO 双按钮 → 分隔线 → 密码/OTP 段切换 → 邮箱 + 密码（或 6 格 OTP）
 *              → 保持登录 + 集群标签 → 主按钮（品牌紫渐变）→ 底部帮助链
 *   固定页脚：© 2026 Stardust Labs / 隐私 / 服务条款 / 状态
 *
 * 行为约定：
 *   - 真实登录：走框架 loginApi.loginByPassword（api-core 统一处理 RSA 挑战 / 字段归一化），
 *     成功后 setAccessToken 写入并 router.replace(redirect)。
 *   - method（password / otp）持久化到 localStorage['stardust-login-method']（对齐设计稿）。
 *   - OTP 通道：后端当前无独立「验证码登录」端点，本页只实现交互与版式，
 *     提交时明确提示未接入，不伪造登录成功。
 *
 * 样式纪律：颜色零硬编码 —— 全部取 tokens.stardust.css 的令牌（--brand-* / --surface-*
 *          / --fg-* / --border-* / --success-500 / --sd-*）；SVG 图形仅用令牌色。
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { fetchLoginConfig, loginByPassword } from '@newlifex/cube-vue/core/utils/loginApi'
import { setAccessToken } from '@newlifex/cube-vue/core/utils/token'
import StarfieldBg from '@/layouts/StardustLayout/starfield/StarfieldBg.vue'
import logoMark from '@/assets/brand/logo-mark.svg'

type LoginMethod = 'password' | 'otp'

const router = useRouter()
const route = useRoute()

/** 设计稿左侧品牌叙事 4 段（原文照录） */
const HIGHLIGHTS = [
  { label: '注册与发现', sub: 'Agent 心跳 200 ms · 亚秒摘除' },
  { label: '配置中心', sub: '灰度发布 · 版本 diff · 3 秒回滚' },
  { label: '远程发布', sub: '蓝绿 / 金丝雀 / 滚动' },
  { label: '性能监控', sub: 'OpenTelemetry 原生兼容' },
]

/** 设计稿右栏集群标签（视觉占位，非后端事实） */
const CLUSTER_LABEL = 'prod-tokyo'

const METHOD_KEY = 'stardust-login-method'

const method = ref<LoginMethod>('password')
const email = ref('')
const password = ref('')
const showPw = ref(false)
const remember = ref(true)
const otp = ref<string[]>(['', '', '', '', '', ''])
const otpRefs = ref<HTMLInputElement[]>([])
const loading = ref(false)
const errorText = ref('')
const tickerIdx = ref(0)

let tickerTimer: ReturnType<typeof setInterval> | null = null

/** 后端登录配置（系统名 / SSO 供应商 / 密码开关），失败不阻塞页面 */
const loginConfig = ref<Record<string, any> | null>(null)
const systemName = computed(() => String(loginConfig.value?.systemName ?? 'Stardust'))
const passwordEnabled = computed(() => loginConfig.value?.login?.password !== false)
const oauthProviders = computed<any[]>(() => {
  const list = loginConfig.value?.oauth
  return Array.isArray(list) ? list : []
})

onMounted(async () => {
  const saved = localStorage.getItem(METHOD_KEY)
  if (saved === 'password' || saved === 'otp') method.value = saved

  tickerTimer = setInterval(() => {
    tickerIdx.value = (tickerIdx.value + 1) % HIGHLIGHTS.length
  }, 3200)

  try {
    const res: any = await fetchLoginConfig()
    loginConfig.value = res?.data ?? null
  } catch {
    // 拉取配置失败不阻塞登录表单（后端不可达时仍可尝试密码登录）
    loginConfig.value = null
  }
})

onBeforeUnmount(() => {
  if (tickerTimer) clearInterval(tickerTimer)
})

/** 切换登录方式并持久化（对齐设计稿 localStorage 约定） */
function setMethod(next: LoginMethod) {
  method.value = next
  errorText.value = ''
  localStorage.setItem(METHOD_KEY, next)
  if (next === 'otp') {
    nextTick(() => otpRefs.value[0]?.focus())
  }
}

/** OTP 单格输入：仅数字、自动跳下一格、退格回退 */
function onOtpInput(i: number, val: string) {
  const digit = val.replace(/\D/g, '').slice(0, 1)
  const next = [...otp.value]
  next[i] = digit
  otp.value = next
  if (digit && i < 5) otpRefs.value[i + 1]?.focus()
}

function onOtpKeydown(i: number, e: KeyboardEvent) {
  if (e.key === 'Backspace' && !otp.value[i] && i > 0) {
    otpRefs.value[i - 1]?.focus()
  }
}

/** 未接入的入口：明确提示，不伪造成功 */
function notifyNotWired(label: string) {
  ElMessage.info(`${label}：当前版本沿用框架既有能力，本页未接入`)
}

function startOAuth(p: any) {
  const name = p?.name ?? p?.scheme ?? ''
  if (!name) {
    notifyNotWired('SSO 登录')
    return
  }
  const redirect = String(route.query.redirect ?? '/stardust/overview')
  window.location.href = `/Sso/Login/${encodeURIComponent(name)}?source=front-end&redirect_uri=${encodeURIComponent(redirect)}`
}

async function submit() {
  if (loading.value) return
  errorText.value = ''

  if (method.value === 'otp') {
    const code = otp.value.join('')
    if (code.length < 6) {
      errorText.value = '请输入 6 位验证码'
      return
    }
    notifyNotWired('一次性验证码登录')
    return
  }

  if (!email.value.trim() || !password.value) {
    errorText.value = '请输入工作邮箱与密码'
    return
  }

  loading.value = true
  try {
    // loginByPassword(username, password, captchaId?, captchaCode?, remember?)
    const res: any = await loginByPassword(
      email.value.trim(),
      password.value,
      undefined,
      undefined,
      remember.value,
    )
    const token = res?.data?.accessToken ?? res?.data?.access_token ?? ''
    if (token) setAccessToken(token)
    const redirect = String(route.query.redirect ?? '/stardust/overview')
    await router.replace(redirect)
  } catch (e: any) {
    errorText.value = e?.message ? String(e.message) : '登录失败，请检查邮箱与密码'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div
    class="sd-login"
    data-spec="login-scene"
  >
    <!-- 全屏星云背景 -->
    <div class="sd-login__bg">
      <StarfieldBg :intensity="1" />
    </div>

    <!-- 居中毛玻璃卡片：1080×660 / 左右 1.1fr : 0.9fr -->
    <div
      class="sd-stage"
      data-spec="login-card"
    >
      <!-- ── 左栏 · 品牌叙事 ───────────────────────────────────────── -->
      <section
        class="sd-brand"
        data-spec="login-brand-side"
      >
        <header class="sd-brand__head">
          <img
            class="sd-brand__mark"
            :src="logoMark"
            width="28"
            height="28"
            alt=""
            aria-hidden="true"
          >
          <span class="sd-brand__name">{{ systemName }}</span>
          <span class="sd-brand__ver">v1.4.0</span>
        </header>

        <div class="sd-brand__center">
          <!-- 轨道核心 220×220 -->
          <div
            class="sd-orbit"
            data-spec="login-orbit"
          >
            <svg
              class="sd-orbit__svg"
              width="220"
              height="220"
              viewBox="0 0 220 220"
              aria-hidden="true"
            >
              <defs>
                <linearGradient
                  id="sdOrbitRing"
                  x1="0"
                  y1="0"
                  x2="1"
                  y2="1"
                >
                  <stop
                    offset="0"
                    class="sd-orbit__stop-a"
                  />
                  <stop
                    offset="1"
                    class="sd-orbit__stop-b"
                  />
                </linearGradient>
              </defs>
              <circle
                class="sd-ring sd-ring--1"
                cx="110"
                cy="110"
                r="105"
              />
              <circle
                class="sd-ring sd-ring--2"
                cx="110"
                cy="110"
                r="82"
              />
              <circle
                class="sd-ring sd-ring--3"
                cx="110"
                cy="110"
                r="56"
              />
              <circle
                class="sd-dot sd-dot--a"
                cx="215"
                cy="110"
                r="3"
              />
              <circle
                class="sd-dot sd-dot--b"
                cx="110"
                cy="28"
                r="4"
              />
              <circle
                class="sd-dot sd-dot--c"
                cx="34"
                cy="140"
                r="3"
              />
            </svg>
            <div
              class="sd-orbit__core"
              data-spec="login-orbit-core"
            >
              <img
                :src="logoMark"
                width="44"
                height="44"
                alt=""
                aria-hidden="true"
              >
            </div>
          </div>

          <div class="sd-brand__claim">
            <div class="sd-brand__title">
              让每一次调用,<br>
              都在<span class="sd-brand__accent">星轨</span>之上
            </div>
            <p class="sd-brand__sub">
              轻量级分布式服务框架 · 4 MB Agent 收敛注册、发现、发布、治理
            </p>
          </div>
        </div>

        <!-- Ticker：固定 48px，3200ms 循环 -->
        <div
          class="sd-ticker"
          data-spec="login-ticker"
        >
          <span
            class="sd-ticker__dot"
            aria-hidden="true"
          />
          <div
            :key="tickerIdx"
            class="sd-ticker__body"
          >
            <div
              class="sd-ticker__label"
              data-spec="login-ticker-item"
            >
              {{ HIGHLIGHTS[tickerIdx].label }}
            </div>
            <div class="sd-ticker__sub">
              {{ HIGHLIGHTS[tickerIdx].sub }}
            </div>
          </div>
          <div
            class="sd-ticker__pager"
            aria-hidden="true"
          >
            <span
              v-for="(h, i) in HIGHLIGHTS"
              :key="h.label"
              class="sd-ticker__pip"
              :class="{ 'is-active': i === tickerIdx }"
            />
          </div>
        </div>
      </section>

      <!-- ── 右栏 · 登录表单 ───────────────────────────────────────── -->
      <section
        class="sd-form"
        data-spec="login-form-side"
      >
        <div class="sd-form__head">
          <h1 class="sd-form__title">
            登录 {{ systemName }} 控制台
          </h1>
          <p class="sd-form__sub">
            使用工作邮箱或 SSO 继续
          </p>
        </div>

        <!-- SSO 双按钮 -->
        <div class="sd-sso">
          <button
            v-for="p in oauthProviders.slice(0, 2)"
            :key="p.name ?? p.title"
            type="button"
            class="sd-sso__btn"
            data-spec="login-sso-btn"
            @click="startOAuth(p)"
          >
            <span class="sd-sso__label">{{ p.title ?? p.displayName ?? p.name }}</span>
          </button>
          <template v-if="!oauthProviders.length">
            <button
              type="button"
              class="sd-sso__btn"
              data-spec="login-sso-btn"
              @click="notifyNotWired('GitHub 登录')"
            >
              <svg
                class="sd-ico"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path
                  d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22"
                />
              </svg>
              <span class="sd-sso__label">GitHub</span>
            </button>
            <button
              type="button"
              class="sd-sso__btn"
              data-spec="login-sso-btn"
              @click="notifyNotWired('SAML SSO')"
            >
              <svg
                class="sd-ico"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path
                  d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"
                />
              </svg>
              <span class="sd-sso__label">SAML SSO</span>
            </button>
          </template>
        </div>

        <!-- 分隔线 -->
        <div class="sd-divider">
          <span>或使用凭证</span>
        </div>

        <!-- 密码 / OTP 段切换 -->
        <div
          class="sd-switch"
          data-spec="login-method-switch"
        >
          <button
            type="button"
            class="sd-switch__btn"
            :class="{ 'is-active': method === 'password' }"
            data-spec="login-method-btn"
            :disabled="!passwordEnabled"
            @click="setMethod('password')"
          >
            密码登录
          </button>
          <button
            type="button"
            class="sd-switch__btn"
            :class="{ 'is-active': method === 'otp' }"
            data-spec="login-method-btn"
            @click="setMethod('otp')"
          >
            一次性验证码
          </button>
        </div>

        <form
          class="sd-fields"
          novalidate
          @submit.prevent="submit"
        >
          <label class="sd-field-group">
            <span class="sd-field-label">工作邮箱</span>
            <span class="sd-field">
              <svg
                class="sd-ico"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <rect
                  x="2"
                  y="4"
                  width="20"
                  height="16"
                  rx="2"
                />
                <path d="M22 6l-10 7L2 6" />
              </svg>
              <input
                v-model="email"
                type="email"
                class="sd-field__input"
                placeholder="you@company.com"
                autocomplete="username"
              >
            </span>
          </label>

          <template v-if="method === 'password'">
            <label class="sd-field-group">
              <span class="sd-field-label">
                密码
                <button
                  type="button"
                  class="sd-field-link"
                  @click="notifyNotWired('忘记密码')"
                >
                  忘记密码?
                </button>
              </span>
              <span class="sd-field">
                <svg
                  class="sd-ico"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                >
                  <rect
                    x="3"
                    y="11"
                    width="18"
                    height="11"
                    rx="2"
                  />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
                <input
                  v-model="password"
                  :type="showPw ? 'text' : 'password'"
                  class="sd-field__input"
                  placeholder="至少 12 位,含大小写与数字"
                  autocomplete="current-password"
                >
                <button
                  type="button"
                  class="sd-field__eye"
                  :class="{ 'is-on': showPw }"
                  aria-label="切换密码可见性"
                  @click="showPw = !showPw"
                >
                  <svg
                    class="sd-ico"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                    <circle
                      cx="12"
                      cy="12"
                      r="3"
                    />
                  </svg>
                </button>
              </span>
            </label>
          </template>

          <template v-else>
            <div class="sd-field-group">
              <span class="sd-field-label">
                一次性验证码
                <button
                  type="button"
                  class="sd-field-link"
                  @click="notifyNotWired('重新发送验证码')"
                >
                  重新发送
                </button>
              </span>
              <div
                class="sd-otp"
                data-spec="login-otp"
              >
                <input
                  v-for="(d, i) in otp"
                  :key="i"
                  :ref="(el) => (otpRefs[i] = el as HTMLInputElement)"
                  class="sd-otp__cell"
                  data-spec="login-otp-cell"
                  :value="d"
                  maxlength="1"
                  inputmode="numeric"
                  @input="onOtpInput(i, ($event.target as HTMLInputElement).value)"
                  @keydown="onOtpKeydown(i, $event)"
                >
              </div>
            </div>
          </template>

          <div class="sd-remember">
            <button
              type="button"
              class="sd-check"
              :class="{ 'is-on': remember }"
              :aria-pressed="remember"
              @click="remember = !remember"
            >
              <svg
                v-if="remember"
                class="sd-ico sd-ico--thick"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path d="M4 12.5l5 5L20 6.5" />
              </svg>
            </button>
            <span
              class="sd-remember__text"
              @click="remember = !remember"
            >保持 7 天登录</span>
            <span class="sd-remember__spacer" />
            <span class="sd-remember__cluster">
              集群 <span class="sd-mono">{{ CLUSTER_LABEL }}</span>
            </span>
          </div>

          <p
            v-if="errorText"
            class="sd-error"
            data-spec="login-error"
          >
            {{ errorText }}
          </p>

          <button
            type="submit"
            class="sd-submit"
            data-spec="login-submit"
            :class="{ 'is-loading': loading }"
            :disabled="loading"
          >
            <span
              v-if="loading"
              class="sd-submit__spin"
              aria-hidden="true"
            />
            <span>{{ loading ? '校验中…' : '进入控制台' }}</span>
          </button>

          <div class="sd-help">
            <span>
              还没有账户?
              <button
                type="button"
                class="sd-field-link"
                @click="notifyNotWired('申请试用')"
              >
                申请试用
              </button>
            </span>
            <span class="sd-help__status">
              <span
                class="sd-help__dot"
                aria-hidden="true"
              />
              所有系统正常
            </span>
          </div>
        </form>
      </section>
    </div>

    <!-- 固定页脚 -->
    <footer
      class="sd-footer"
      data-spec="login-footer"
    >
      <span>© 2026 Stardust Labs</span>
      <button
        type="button"
        class="sd-footer__link"
        @click="notifyNotWired('隐私政策')"
      >
        隐私
      </button>
      <button
        type="button"
        class="sd-footer__link"
        @click="notifyNotWired('服务条款')"
      >
        服务条款
      </button>
      <button
        type="button"
        class="sd-footer__link"
        @click="notifyNotWired('系统状态')"
      >
        状态
      </button>
    </footer>
  </div>
</template>

<style scoped>
/* ── 场景 ─────────────────────────────────────────────────────────────── */
.sd-login {
  position: relative;
  min-height: 100vh;
  width: 100%;
  background: var(--bg);
  color: var(--fg-1);
  font-family: var(--font-sans);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}
.sd-login__bg {
  position: fixed;
  inset: 0;
  z-index: 0;
  pointer-events: none;
}

/* ── 卡片 ─────────────────────────────────────────────────────────────── */
.sd-stage {
  position: relative;
  z-index: 2;
  width: 1080px;
  height: 660px;
  display: grid;
  grid-template-columns: 1.1fr 0.9fr;
  border-radius: var(--radius-2xl);
  border: 1px solid var(--border-1);
  background: color-mix(in srgb, var(--surface-1) 72%, transparent);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  box-shadow: var(--elev-2);
  overflow: hidden;
}
@media (max-width: 1120px), (max-height: 760px) {
  .sd-stage {
    width: 100%;
    max-width: 1080px;
    height: auto;
    min-height: 0;
    grid-template-columns: 1fr;
  }
}

/* ── 左栏 · 品牌 ──────────────────────────────────────────────────────── */
.sd-brand {
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: var(--space-5);
  padding: var(--space-6);
  border-right: 1px solid var(--border-1);
}
.sd-brand__head {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
.sd-brand__mark {
  filter: drop-shadow(0 0 8px color-mix(in srgb, var(--brand-300) 60%, transparent));
}
.sd-brand__name {
  font-size: 15px;
  font-weight: 600;
  letter-spacing: -0.005em;
  color: var(--fg-1);
}
.sd-brand__ver {
  margin-left: var(--space-1);
  padding: 2px 8px;
  border-radius: var(--radius-pill);
  background: color-mix(in srgb, var(--brand-500) 15%, transparent);
  border: 1px solid color-mix(in srgb, var(--brand-500) 30%, transparent);
  font-family: var(--font-mono);
  font-size: 10.5px;
  font-weight: 500;
  color: var(--brand-300);
}
.sd-brand__center {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-6);
}
.sd-brand__claim {
  max-width: 340px;
  text-align: center;
}
.sd-brand__title {
  font-size: 26px;
  font-weight: 600;
  line-height: 1.25;
  letter-spacing: -0.02em;
  color: var(--fg-1);
}
.sd-brand__accent {
  background: var(--sd-gradient-brand);
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
  font-weight: 700;
}
.sd-brand__sub {
  margin-top: var(--space-3);
  font-size: 13px;
  line-height: 1.6;
  color: var(--fg-3);
}

/* ── 轨道核心 ─────────────────────────────────────────────────────────── */
.sd-orbit {
  position: relative;
  width: 220px;
  height: 220px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.sd-orbit__svg {
  position: absolute;
  inset: 0;
}
.sd-orbit__stop-a {
  stop-color: var(--brand-300);
}
.sd-orbit__stop-b {
  stop-color: var(--cyan-500);
}
.sd-ring {
  fill: none;
  stroke: url(#sdOrbitRing);
}
.sd-ring--1 {
  stroke-width: 1;
  opacity: 0.35;
}
.sd-ring--2 {
  stroke-width: 1;
  stroke-dasharray: 3 6;
  opacity: 0.45;
}
.sd-ring--3 {
  stroke-width: 1;
  opacity: 0.55;
}
.sd-dot--a {
  fill: var(--brand-300);
  color: var(--brand-300);
}
.sd-dot--b {
  fill: var(--cyan-500);
  color: var(--cyan-500);
}
.sd-dot--c {
  fill: var(--brand-400);
  color: var(--brand-400);
}
.sd-dot {
  filter: drop-shadow(0 0 6px currentColor);
}
.sd-orbit__core {
  position: relative;
  z-index: 2;
  width: 96px;
  height: 96px;
  border-radius: 50%;
  border: 1.5px solid var(--brand-400);
  background: radial-gradient(circle at 40% 40%, var(--brand-800) 0%, var(--surface-1) 75%);
  box-shadow: var(--sd-glow-core);
  display: flex;
  align-items: center;
  justify-content: center;
  animation: sdCorePulse 3s ease-in-out infinite;
}
.sd-orbit__core img {
  filter: brightness(1.5) drop-shadow(0 0 8px color-mix(in srgb, var(--brand-300) 80%, transparent));
}
@keyframes sdCorePulse {
  0%,
  100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.04);
  }
}

/* ── Ticker ───────────────────────────────────────────────────────────── */
.sd-ticker {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  height: 48px;
  padding: 6px 14px;
  border-radius: var(--radius-lg);
  background: color-mix(in srgb, var(--fg-1) 3%, transparent);
  border: 1px solid var(--border-1);
  overflow: hidden;
}
.sd-ticker__dot {
  width: 6px;
  height: 6px;
  flex-shrink: 0;
  border-radius: var(--radius-pill);
  background: var(--success-500);
  box-shadow: 0 0 8px var(--success-500);
}
.sd-ticker__body {
  flex: 1;
  min-width: 0;
  animation: sdTickerIn var(--dur-medium) var(--ease-out);
}
.sd-ticker__label {
  font-size: 12.5px;
  font-weight: 500;
  line-height: 1.25;
  color: var(--fg-1);
}
.sd-ticker__sub {
  margin-top: 1px;
  font-family: var(--font-mono);
  font-size: 10.5px;
  line-height: 1.25;
  color: var(--fg-3);
}
.sd-ticker__pager {
  display: flex;
  gap: 4px;
}
.sd-ticker__pip {
  width: 4px;
  height: 4px;
  border-radius: var(--radius-pill);
  background: var(--surface-3);
  transition: width var(--dur-medium) var(--ease-out), background var(--dur-medium) var(--ease-out);
}
.sd-ticker__pip.is-active {
  width: 14px;
  background: var(--brand-500);
}
@keyframes sdTickerIn {
  from {
    opacity: 0;
    transform: translateY(6px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* ── 右栏 · 表单 ──────────────────────────────────────────────────────── */
.sd-form {
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: var(--space-6);
  min-width: 0;
}
.sd-form__head {
  margin-bottom: var(--space-6);
}
.sd-form__title {
  font-size: 22px;
  font-weight: 600;
  letter-spacing: -0.015em;
  color: var(--fg-1);
}
.sd-form__sub {
  margin-top: 6px;
  font-size: 13px;
  color: var(--fg-3);
}

/* SSO */
.sd-sso {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 10px;
  margin-bottom: var(--space-5);
}
.sd-sso__btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: 48px;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--surface-1);
  border: 1px solid var(--border-2);
  color: var(--fg-1);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), border-color var(--dur-micro) var(--ease-out);
}
.sd-sso__btn:hover {
  background: var(--surface-2);
  border-color: var(--brand-500);
}
.sd-sso__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 分隔线 */
.sd-divider {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: var(--space-5);
  color: var(--fg-3);
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}
.sd-divider::before,
.sd-divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--border-1);
}

/* 段切换 */
.sd-switch {
  display: grid;
  grid-template-columns: 1fr 1fr;
  padding: 3px;
  border-radius: var(--radius-md);
  background: var(--surface-2);
  border: 1px solid var(--border-1);
  margin-bottom: var(--space-5);
}
.sd-switch__btn {
  padding: 7px 12px;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--fg-2);
  font-size: 12.5px;
  font-weight: 500;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.sd-switch__btn.is-active {
  background: var(--brand-500);
  color: var(--fg-1);
}
.sd-switch__btn:disabled {
  color: var(--fg-4);
  cursor: not-allowed;
}

/* 字段 */
.sd-fields {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
.sd-field-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.sd-field-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  font-size: 12px;
  font-weight: 500;
  color: var(--fg-2);
}
.sd-field-link {
  padding: 0;
  border: none;
  background: none;
  font-size: 11.5px;
  color: var(--brand-300);
  cursor: pointer;
  white-space: nowrap;
}
.sd-field-link:hover {
  color: var(--brand-200);
}
.sd-field {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--surface-1);
  border: 1px solid var(--border-2);
  transition: border-color var(--dur-micro) var(--ease-out), box-shadow var(--dur-micro) var(--ease-out);
}
.sd-field:focus-within {
  border-color: var(--brand-500);
  box-shadow: var(--sd-brand-ring);
}
.sd-field__input {
  flex: 1;
  min-width: 0;
  background: transparent;
  border: none;
  outline: none;
  color: var(--fg-1);
  font-family: var(--font-sans);
  font-size: 14px;
}
.sd-field__input::placeholder {
  color: var(--fg-4);
}
.sd-field__eye {
  display: flex;
  padding: 2px;
  border: none;
  background: none;
  color: var(--fg-3);
  cursor: pointer;
}
.sd-field__eye.is-on {
  color: var(--brand-300);
}
.sd-ico {
  width: 15px;
  height: 15px;
  flex-shrink: 0;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.5;
  stroke-linecap: round;
  stroke-linejoin: round;
  color: var(--fg-3);
}
.sd-field:focus-within .sd-ico {
  color: var(--brand-300);
}
.sd-ico--thick {
  stroke-width: 3;
  color: var(--fg-1);
}

/* OTP */
.sd-otp {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: var(--space-2);
}
.sd-otp__cell {
  width: 100%;
  height: 44px;
  padding: 0;
  text-align: center;
  border-radius: var(--radius-md);
  background: var(--surface-1);
  border: 1px solid var(--border-2);
  outline: none;
  color: var(--fg-1);
  font-family: var(--font-mono);
  font-size: 18px;
  font-weight: 500;
  transition: border-color var(--dur-micro) var(--ease-out), box-shadow var(--dur-micro) var(--ease-out);
}
.sd-otp__cell:focus {
  border-color: var(--brand-500);
  box-shadow: var(--sd-brand-ring);
}

/* 保持登录 */
.sd-remember {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: 12.5px;
  color: var(--fg-2);
}
.sd-check {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
  padding: 0;
  border-radius: var(--radius-xs);
  background: var(--surface-1);
  border: 1px solid var(--border-2);
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), border-color var(--dur-micro) var(--ease-out);
}
.sd-check.is-on {
  background: var(--brand-500);
  border-color: var(--brand-500);
}
.sd-check .sd-ico--thick {
  width: 11px;
  height: 11px;
}
.sd-remember__text {
  cursor: pointer;
  user-select: none;
  white-space: nowrap;
}
.sd-remember__spacer {
  flex: 1;
}
.sd-remember__cluster {
  font-size: 11px;
  color: var(--fg-3);
  white-space: nowrap;
}
.sd-mono {
  margin-left: 4px;
  font-family: var(--font-mono);
  color: var(--fg-1);
}

/* 错误 */
.sd-error {
  padding: 8px 10px;
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--danger-500) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--danger-500) 32%, transparent);
  font-size: 12.5px;
  color: var(--danger-500);
}

/* 主按钮 */
.sd-submit {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: 46px;
  padding: 12px 16px;
  border: none;
  border-radius: var(--radius-md);
  background: var(--sd-gradient-btn);
  color: var(--fg-1);
  font-family: var(--font-sans);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  box-shadow: var(--sd-glow-btn);
  transition: filter var(--dur-micro) var(--ease-out), box-shadow var(--dur-micro) var(--ease-out);
}
.sd-submit:hover:not(:disabled) {
  filter: brightness(1.08);
}
.sd-submit.is-loading,
.sd-submit:disabled {
  background: var(--brand-700);
  box-shadow: none;
  cursor: default;
}
.sd-submit__spin {
  width: 14px;
  height: 14px;
  border-radius: var(--radius-pill);
  border: 2px solid color-mix(in srgb, var(--fg-1) 35%, transparent);
  border-top-color: var(--fg-1);
  animation: sdSpin 700ms linear infinite;
}
@keyframes sdSpin {
  to {
    transform: rotate(360deg);
  }
}

/* 帮助行 */
.sd-help {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  margin-top: var(--space-1);
  font-size: 12px;
  color: var(--fg-3);
}
.sd-help__status {
  display: flex;
  align-items: center;
  gap: 4px;
  white-space: nowrap;
}
.sd-help__dot {
  width: 5px;
  height: 5px;
  border-radius: var(--radius-pill);
  background: var(--success-500);
  box-shadow: 0 0 4px var(--success-500);
}

/* ── 页脚 ─────────────────────────────────────────────────────────────── */
.sd-footer {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 20px;
  z-index: 3;
  display: flex;
  justify-content: center;
  gap: 20px;
  font-size: 11px;
  color: var(--fg-4);
}
.sd-footer__link {
  padding: 0;
  border: none;
  background: none;
  font-family: inherit;
  font-size: 11px;
  color: var(--fg-4);
  cursor: pointer;
}
.sd-footer__link:hover {
  color: var(--fg-3);
}
</style>
