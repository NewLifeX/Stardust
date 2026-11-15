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
 *     └ 右栏：标题 → 用户名 + 密码 → 第三方登录
 *              → 保持登录 + 集群标签 → 主按钮（品牌紫渐变）→ 底部帮助链
 *   固定页脚：© 2026 Stardust Labs / 隐私 / 服务条款 / 状态
 *
 * 行为约定（皮在本页，能力复用 Cube LoginPage）：
 *   配置来自 fetchLoginConfig。第三方只渲染 oauth，不写死 GitHub / SAML。
 *   跳转与 Cube handleOAuth 相同：/Sso/Login/{name}?source=front-end&redirect_uri=
 *   密码规则复用 usePasswordRules；成功条件与 handleLogin 相同（code 0 且有 accessToken）。
 *
 * 样式纪律：颜色零硬编码 —— 全部取 tokens.stardust.css 的令牌（--brand-* / --surface-*
 *          / --fg-* / --border-* / --success-500 / --sd-*）；SVG 图形仅用令牌色。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { fetchLoginConfig, loginByPassword, registerByForm, sendActivateCode, sendCode } from '@newlifex/cube-vue/core/utils/loginApi'
import { setAccessToken, setRefreshToken } from '@newlifex/cube-vue/core/utils/token'
import { usePasswordRules } from '@newlifex/cube-vue/core/composables/usePasswordRules'
import StarfieldBg from '@/layouts/StardustLayout/starfield/StarfieldBg.vue'
import logoMark from '@/assets/brand/logo-mark.svg'

const router = useRouter()
const route = useRoute()

/** 设计稿左侧品牌叙事 4 段（原文照录） */
const HIGHLIGHTS = [
  { label: '注册与发现', sub: 'Agent 心跳 200 ms · 亚秒摘除' },
  { label: '配置中心', sub: '灰度发布 · 版本 diff · 3 秒回滚' },
  { label: '远程发布', sub: '蓝绿 / 金丝雀 / 滚动' },
  { label: '性能监控', sub: 'OpenTelemetry 原生兼容' },
]

type RegMode = 'password' | 'mail' | 'mobile'

const panel = ref<'login' | 'register'>('login')
const regMode = ref<RegMode>('password')
const regUsername = ref('')
const regEmail = ref('')
const regMobile = ref('')
const regCode = ref('')
const regPassword = ref('')
const regConfirm = ref('')
const showRegPw = ref(false)
const username = ref('')
const password = ref('')
const showPw = ref(false)
const remember = ref(true)
const loading = ref(false)
const errorText = ref('')
const notActivated = ref(false)
const notActivatedAccount = ref('')
const pendingActivation = ref<{ channels?: string[]; targets?: string[] } | null>(null)

const tenant = computed(() => {
  const value = route.query.tenant
  const text = Array.isArray(value) ? value[0] : value
  return text || undefined
})
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

/** 与 LoginForm 同一套规则：强度正则来自 security.passwordStrength */
/** Cube register：password / mail / sms 各自一种方式，没开任何一种时走用户名密码 */
const registerModes = computed<RegMode[]>(() => {
  const ability = loginConfig.value?.register
  const modes: RegMode[] = []
  if (ability?.password === true) modes.push('password')
  if (ability?.mail === true) modes.push('mail')
  if (ability?.sms === true) modes.push('mobile')
  if (!modes.length) modes.push('password')
  return modes
})
const requireMailVerify = computed(() => loginConfig.value?.register?.requireMailVerify === true)
const requireMobileVerify = computed(() => loginConfig.value?.register?.requireMobileVerify === true)

const { passwordRuleDefs, passwordRules, showPasswordHints } = usePasswordRules(
  () => loginConfig.value?.security?.passwordStrength,
  () => password.value,
  () => loginConfig.value?.security?.passwordComplexity !== false,
)
const {
  passwordRuleDefs: regRuleDefs,
  passwordRules: regPasswordRules,
  showPasswordHints: showRegPasswordHints,
} = usePasswordRules(
  () => loginConfig.value?.security?.passwordStrength,
  () => regPassword.value,
  () => loginConfig.value?.security?.passwordComplexity !== false,
)

onMounted(async () => {
  tickerTimer = setInterval(() => {
    tickerIdx.value = (tickerIdx.value + 1) % HIGHLIGHTS.length
  }, 3200)

  try {
    const res: any = await fetchLoginConfig(tenant.value)
    if (res?.code === 0 && res.data) {
      loginConfig.value = res.data
      const providers = Array.isArray(res.data.oauth) ? res.data.oauth : []
      // Cube：密码未开且只有一个第三方时直接跳转
      if (res.data.login?.password !== true && providers.length === 1) {
        startOAuth(providers[0])
      }
    } else {
      loginConfig.value = null
      errorText.value = res?.message || '获取登录配置失败'
    }
  } catch {
    // 拉取配置失败不阻塞登录表单（后端不可达时仍可尝试密码登录）
    loginConfig.value = null
    errorText.value = '获取登录配置失败'
  }
})

onBeforeUnmount(() => {
  if (tickerTimer) clearInterval(tickerTimer)
})

/** 未接入的入口：明确提示，不伪造成功 */
function notifyNotWired(label: string) {
  ElMessage.info(`${label}：当前版本沿用框架既有能力，本页未接入`)
}

/** 与 Cube resolveLogo 相同。开发时静态图在后端主机上，相对路径拼上 VITE_APP_BASE_API。 */
function apiOrigin(): string {
  return String(import.meta.env.VITE_APP_BASE_API ?? '').replace(/\/$/, '')
}

function resolveLogo(logo: string): string {
  if (!logo) return ''
  if (/^https?:\/\//i.test(logo)) return logo
  if (logo.startsWith('//')) return `${window.location.protocol}${logo}`
  return logo.startsWith('/') ? `${apiOrigin()}${logo}` : logo
}

function providerName(p: any): string {
  return String(p?.name ?? p?.scheme ?? '')
}

function isGithub(p: any): boolean {
  return providerName(p).toLowerCase() === 'github'
}

function providerLabel(p: any): string {
  return String(p?.nickName || p?.name || '')
}

/** 与 Cube handleOAuth / buildOAuthUrl 相同。默认回跳星尘总览。 */
function startOAuth(p: any) {
  const name = providerName(p)
  if (!name) {
    notifyNotWired('SSO 登录')
    return
  }
  const raw = route.query.redirect
  const text = typeof raw === 'string' ? raw : ''
  const path = text.startsWith('/') ? text : '/stardust/overview'
  const redirectUri = /^https?:\/\//i.test(text) ? text : `${window.location.origin}${path}`
  window.location.href = `${apiOrigin()}/Sso/Login/${name}?source=front-end&redirect_uri=${encodeURIComponent(redirectUri)}`
}

async function submit() {
  if (loading.value) return
  errorText.value = ''

  if (!passwordEnabled.value) {
    errorText.value = '当前未开放密码登录'
    return
  }

  const name = username.value.trim()
  if (!name || !password.value) {
    errorText.value = '请输入用户名与密码'
    return
  }
  if (name.length < 2) {
    errorText.value = '用户名至少 2 个字符'
    return
  }

  const failed = passwordRuleDefs.value.find((rule) => !rule.test(password.value))
  if (failed) {
    errorText.value = `密码需${failed.label}`
    return
  }

  loading.value = true
  try {
    // loginByPassword(username, password, captchaId?, captchaCode?, remember?)
    const res: any = await loginByPassword(
      name,
      password.value,
      undefined,
      undefined,
      remember.value,
    )
    const token = res?.data?.accessToken ?? ''
    if (res?.code !== 0 || !token) {
      const message = res?.message ? String(res.message) : '登录失败，请检查用户名与密码'
      if (message.includes('未激活')) {
        notActivated.value = true
        notActivatedAccount.value = name
      }
      errorText.value = message
      return
    }
    setAccessToken(token)
    if (res.data?.refreshToken) setRefreshToken(res.data.refreshToken)
    ElMessage.success('登录成功，正在跳转...')
    const redirect = String(route.query.redirect ?? '/stardust/overview')
    await router.replace(redirect)
  } catch (e: any) {
    const message = e?.message ? String(e.message) : '登录失败，请检查用户名与密码'
    if (message.includes('未激活')) {
      notActivated.value = true
      notActivatedAccount.value = name
    }
    errorText.value = message
  } finally {
    loading.value = false
  }
}

function showRegister() {
  panel.value = 'register'
  errorText.value = ''
  notActivated.value = false
  regMode.value = registerModes.value[0]
}

function showLogin() {
  panel.value = 'login'
  errorText.value = ''
  pendingActivation.value = null
}

function isMail(value: string): boolean {
  return /^[\w.+-]+@[\w-]+(\.[\w-]+)+$/.test(value)
}

function isMobile(value: string): boolean {
  return /^1[3-9]\d{9}$/.test(value)
}

/** 与 Cube RegisterForm 相同的类别和校验，一次只提交当前方式 */
async function submitRegister() {
  if (loading.value) return
  errorText.value = ''
  const mode = regMode.value
  const name = regUsername.value.trim()
  const email = regEmail.value.trim()
  const mobile = regMobile.value.trim()
  const code = regCode.value.trim()

  if (mode === 'password') {
    if (!name) {
      errorText.value = '请输入用户名'
      return
    }
    if (name.length < 2) {
      errorText.value = '用户名至少 2 个字符'
      return
    }
  }
  if (mode === 'mail' || (mode === 'password' && requireMailVerify.value)) {
    if (!email) {
      errorText.value = '请输入邮箱'
      return
    }
    if (!isMail(email)) {
      errorText.value = '邮箱格式不正确'
      return
    }
  }
  if (mode === 'mobile' || (mode === 'password' && requireMobileVerify.value)) {
    if (!mobile) {
      errorText.value = '请输入手机号'
      return
    }
    if (!isMobile(mobile)) {
      errorText.value = '手机号格式不正确'
      return
    }
  }
  if ((mode === 'mail' || mode === 'mobile') && !code) {
    errorText.value = '请输入验证码'
    return
  }
  if (!regPassword.value) {
    errorText.value = '请输入密码'
    return
  }
  const failed = regRuleDefs.value.find((rule) => !rule.test(regPassword.value))
  if (failed) {
    errorText.value = `密码需${failed.label}`
    return
  }
  if (regPassword.value !== regConfirm.value) {
    errorText.value = '两次密码不一致'
    return
  }

  loading.value = true
  try {
    const res: any = await registerByForm({
      category: mode === 'password' ? undefined : mode,
      username: mode === 'password' ? name : undefined,
      email: email || undefined,
      mobile: mobile || undefined,
      password: regPassword.value,
      confirmPassword: regConfirm.value,
      code: mode === 'mail' || mode === 'mobile' ? code : undefined,
    })
    if (res?.code === 0 && res.data?.accessToken) {
      setAccessToken(res.data.accessToken)
      if (res.data.refreshToken) setRefreshToken(res.data.refreshToken)
      ElMessage.success('注册成功')
      await router.replace(String(route.query.redirect ?? '/stardust/overview'))
      return
    }
    if (res?.code === 0 && res.data?.pendingActivation) {
      pendingActivation.value = res.data
      return
    }
    if (res?.code === 0) {
      ElMessage.success(res.message || '注册成功')
      showLogin()
      return
    }
    ElMessage.error(res?.message || '注册失败')
  } catch (e: any) {
    ElMessage.error(e?.message ? String(e.message) : '注册失败')
  } finally {
    loading.value = false
  }
}

function detectChannel(account: string): 'mail' | 'sms' {
  return /^1[3-9]\d{9}$/.test(account) ? 'sms' : 'mail'
}

async function doResend(channel: 'mail' | 'sms', account: string) {
  try {
    const res: any = await sendActivateCode(channel === 'mail' ? 'Mail' : 'Sms', account)
    if (res?.code === 0) ElMessage.success(res.message || '激活邮件/短信已重新发送，请查收')
    else ElMessage.error(res?.message || '重新发送失败')
  } catch (e: any) {
    ElMessage.error(e?.message ? String(e.message) : '重新发送失败')
  }
}

/** 与 Cube openResend 相同：弹出输入邮箱或手机号 */
async function openResend() {
  try {
    const { value } = await ElMessageBox.prompt(
      '请输入注册时填写的邮箱或手机号',
      '重新发送激活',
      {
        inputValue: notActivatedAccount.value || '',
        inputPattern: /^([\w.+-]+@[\w-]+(\.[\w-]+)+|1[3-9]\d{9})$/,
        inputErrorMessage: '请输入正确的邮箱或手机号',
        confirmButtonText: '发送',
        cancelButtonText: '取消',
      },
    )
    if (value) await doResend(detectChannel(value), value.trim())
  } catch {
    // 用户取消
  }
}

async function resendPending() {
  const account = pendingActivation.value?.targets?.[0]
  if (!account) return
  await doResend(detectChannel(account), account)
}

/** 与 Cube LoginPage.handleSendCode 相同 */
async function sendRegisterCode(channel: 'mail' | 'sms', account: string) {
  if (channel === 'mail' && !isMail(account)) {
    errorText.value = account ? '邮箱格式不正确' : '请输入邮箱'
    return
  }
  if (channel === 'sms' && !isMobile(account)) {
    errorText.value = account ? '手机号格式不正确' : '请输入手机号'
    return
  }
  try {
    const res: any = await sendCode(channel === 'mail' ? 'Mail' : 'Sms', account, 'register')
    if (res?.code === 0) ElMessage.success('验证码已发送')
    else ElMessage.error(res?.message || '验证码发送失败')
  } catch (e: any) {
    ElMessage.error(e?.message ? String(e.message) : '验证码发送失败')
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
            {{ panel === 'login' ? '登录' : '注册' }} {{ systemName }} 控制台
          </h1>
        </div>

        <div class="sd-form__panels">
          <div
            class="sd-form__panel"
            :class="{ 'is-off': panel !== 'login' }"
            :inert="panel !== 'login'"
          >
            <form
              class="sd-fields"
              novalidate
              @submit.prevent="submit"
            >
              <label class="sd-field-group">
                <span class="sd-field-label">用户名</span>
                <span class="sd-field">
                  <svg
                    class="sd-ico"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <circle
                      cx="12"
                      cy="8"
                      r="4"
                    />
                    <path d="M20 21a8 8 0 0 0-16 0" />
                  </svg>
                  <input
                    v-model="username"
                    type="text"
                    class="sd-field__input"
                    placeholder="请输入用户名"
                    autocomplete="username"
                  >
                </span>
              </label>

              <label
                v-if="passwordEnabled"
                class="sd-field-group"
              >
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
                    placeholder="请输入密码"
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
                <div
                  v-if="showPasswordHints"
                  class="password-hints"
                  data-spec="password-hints"
                >
                  <div
                    v-for="(rule, idx) in passwordRules"
                    :key="idx"
                    class="password-hint-item"
                    :class="{ satisfied: rule.satisfied }"
                  >
                    <span class="hint-icon">{{ rule.satisfied ? '✓' : '○' }}</span>
                    <span class="hint-text">{{ rule.label }}</span>
                  </div>
                </div>
              </label>

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
                >记住我</span>
              </div>

              <p
                v-if="errorText"
                class="sd-error"
                data-spec="login-error"
              >
                {{ errorText }}
              </p>
              <p
                v-if="notActivated"
                class="sd-error"
                data-spec="login-not-activated"
              >
                账号未激活，请先激活后再登录
                <button
                  type="button"
                  class="sd-field-link"
                  @click="openResend"
                >
                  重新发送激活
                </button>
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
                    @click="showRegister"
                  >
                    去注册
                  </button>
                </span>
              </div>
            </form>

            <!-- 第三方在凭证下方，只渲染 LoginConfig.oauth -->
            <template v-if="oauthProviders.length">
              <div class="sd-divider">
                <span>或第三方登录</span>
              </div>
              <div class="sd-sso">
                <button
                  v-for="p in oauthProviders"
                  :key="providerName(p)"
                  type="button"
                  class="sd-sso__btn"
                  data-spec="login-sso-btn"
                  :aria-label="providerLabel(p)"
                  :title="providerLabel(p)"
                  @click="startOAuth(p)"
                >
                  <img
                    v-if="p.logo"
                    class="sd-sso__logo"
                    :src="resolveLogo(p.logo)"
                    alt=""
                  >
                  <svg
                    v-else-if="isGithub(p)"
                    class="sd-ico"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <path
                      d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22"
                    />
                  </svg>
                  <span
                    v-else
                    class="sd-sso__mark"
                  >{{ providerLabel(p).slice(0, 1) }}</span>
                </button>
              </div>
            </template>
          </div>

          <div
            class="sd-register sd-form__panel"
            :class="{ 'is-off': panel !== 'register' }"
            :inert="panel !== 'register'"
            data-spec="register-panel"
          >
            <div
              v-if="pendingActivation"
              class="sd-fields"
              data-spec="register-pending"
            >
              <p class="sd-field-label">
                注册成功，请激活账号
              </p>
              <p class="sd-error">
                激活{{ pendingActivation.channels?.includes('mail') ? '邮件' : '短信' }}已发送至 {{ pendingActivation.targets?.join(', ') }}，请查收并完成激活。激活前无法登录。
              </p>
              <button
                type="button"
                class="sd-submit"
                @click="showLogin"
              >
                返回登录
              </button>
              <button
                v-if="pendingActivation.targets?.length"
                type="button"
                class="sd-field-link"
                @click="resendPending"
              >
                重新发送
              </button>
            </div>
            <div
              v-if="!pendingActivation && registerModes.length > 1"
              class="sd-switch"
              :style="{ gridTemplateColumns: `repeat(${registerModes.length}, 1fr)` }"
            >
              <button
                v-for="mode in registerModes"
                :key="mode"
                type="button"
                class="sd-switch__btn"
                :class="{ 'is-active': regMode === mode }"
                @click="regMode = mode; errorText = ''"
              >
                {{ mode === 'password' ? '用户名' : mode === 'mail' ? '邮箱' : '手机' }}
              </button>
            </div>
            <form
              v-if="!pendingActivation"
              class="sd-fields"
              novalidate
              @submit.prevent="submitRegister"
            >
              <label
                v-if="regMode === 'password'"
                class="sd-field-group"
              >
                <span class="sd-field-label">用户名</span>
                <span class="sd-field">
                  <svg
                    class="sd-ico"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <circle
                      cx="12"
                      cy="8"
                      r="4"
                    />
                    <path d="M20 21a8 8 0 0 0-16 0" />
                  </svg>
                  <input
                    v-model="regUsername"
                    type="text"
                    class="sd-field__input"
                    placeholder="请输入用户名"
                    autocomplete="username"
                  >
                </span>
              </label>
              <label
                v-if="regMode === 'mail' || (regMode === 'password' && requireMailVerify)"
                class="sd-field-group"
              >
                <span class="sd-field-label">
                  邮箱
                  <button
                    v-if="regMode === 'mail'"
                    type="button"
                    class="sd-field-link"
                    @click="sendRegisterCode('mail', regEmail.trim())"
                  >
                    发送验证码
                  </button>
                </span>
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
                    v-model="regEmail"
                    type="email"
                    class="sd-field__input"
                    placeholder="请输入邮箱"
                    autocomplete="email"
                  >
                </span>
              </label>
              <label
                v-if="regMode === 'mobile' || (regMode === 'password' && requireMobileVerify)"
                class="sd-field-group"
              >
                <span class="sd-field-label">
                  手机号
                  <button
                    v-if="regMode === 'mobile'"
                    type="button"
                    class="sd-field-link"
                    @click="sendRegisterCode('sms', regMobile.trim())"
                  >
                    发送验证码
                  </button>
                </span>
                <span class="sd-field">
                  <input
                    v-model="regMobile"
                    type="tel"
                    class="sd-field__input"
                    placeholder="请输入手机号"
                    autocomplete="tel"
                  >
                </span>
              </label>
              <label
                v-if="regMode !== 'password'"
                class="sd-field-group"
              >
                <span class="sd-field-label">验证码</span>
                <span class="sd-field">
                  <input
                    v-model="regCode"
                    type="text"
                    class="sd-field__input"
                    placeholder="请输入验证码"
                    maxlength="6"
                    inputmode="numeric"
                  >
                </span>
              </label>
              <label class="sd-field-group">
                <span class="sd-field-label">密码</span>
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
                    v-model="regPassword"
                    :type="showRegPw ? 'text' : 'password'"
                    class="sd-field__input"
                    placeholder="请输入密码"
                    autocomplete="new-password"
                  >
                </span>
              </label>
              <label class="sd-field-group">
                <span class="sd-field-label">确认密码</span>
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
                    v-model="regConfirm"
                    :type="showRegPw ? 'text' : 'password'"
                    class="sd-field__input"
                    placeholder="再次输入密码"
                    autocomplete="new-password"
                  >
                </span>
              </label>
              <div
                v-if="showRegPasswordHints"
                class="password-hints"
                data-spec="register-password-hints"
              >
                <div
                  v-for="(rule, idx) in regPasswordRules"
                  :key="idx"
                  class="password-hint-item"
                  :class="{ satisfied: rule.satisfied }"
                >
                  <span class="hint-icon">{{ rule.satisfied ? '✓' : '○' }}</span>
                  <span class="hint-text">{{ rule.label }}</span>
                </div>
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
                data-spec="register-submit"
                :disabled="loading"
              >
                <span>{{ loading ? '提交中…' : '注册' }}</span>
              </button>
            </form>
            <div class="sd-help">
              <span>
                已有账户?
                <button
                  type="button"
                  class="sd-field-link"
                  @click="showLogin"
                >
                  去登录
                </button>
              </span>
            </div>
          </div>
        </div>
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
  min-height: 0;
}
.sd-form__panels {
  display: grid;
}
.sd-form__panel {
  grid-area: 1 / 1;
  align-self: start;
  min-width: 0;
}
.sd-form__panel.is-off {
  visibility: hidden;
  pointer-events: none;
}
.sd-register {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}
.password-hints {
  margin-top: 6px;
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.password-hint-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  line-height: 1.5;
  color: var(--fg-3);
}
.password-hint-item.satisfied {
  color: var(--success-500);
}
.hint-icon {
  width: 14px;
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

/* SSO：每家一个圆，整组居中，不拉成长条 */
.sd-sso {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: var(--space-5);
}
.sd-sso__btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  padding: 0;
  border-radius: 50%;
  background: var(--surface-1);
  border: 1px solid var(--border-2);
  color: var(--fg-1);
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), border-color var(--dur-micro) var(--ease-out);
}
.sd-sso__btn:hover {
  background: var(--surface-2);
  border-color: var(--brand-500);
}
.sd-sso__logo {
  width: 22px;
  height: 22px;
  object-fit: contain;
}
.sd-sso__btn .sd-ico {
  width: 20px;
  height: 20px;
}
.sd-sso__mark {
  font-size: 14px;
  font-weight: 600;
  line-height: 1;
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
