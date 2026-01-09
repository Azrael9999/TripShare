<template>
  <div class="mx-auto max-w-5xl px-4 py-10">
    <div class="flex items-center justify-between gap-4">
      <div>
        <div class="text-2xl font-semibold">Sign in</div>
        <p class="text-sm text-slate-600 mt-1">
          Trips can be created and booked only after verifying both email and phone.
        </p>
      </div>
      <RouterLink to="/" class="btn-ghost">Back to home</RouterLink>
    </div>

    <div class="mt-6 grid grid-cols-1 lg:grid-cols-[220px_1fr] gap-6 items-start">
      <div class="hidden lg:block self-start">
        <div class="w-full min-h-[180px] bg-gradient-to-br from-brand-50 to-white rounded-2xl flex items-center justify-center border border-slate-100 p-4">
          <img v-if="brandLoginIllustration" :src="brandLoginIllustration" alt="Sign-in illustration" class="max-h-40 object-contain" />
          <div v-else class="text-xs text-slate-500 text-center px-3">Secure sign-in</div>
        </div>
      </div>

      <div class="card p-6 space-y-6">
        <div class="rounded-2xl border border-slate-100 bg-slate-50/60 p-4">
          <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p class="text-xs uppercase tracking-wide text-slate-500">Google sign-in</p>
              <p class="text-sm text-slate-600">Continue with Google to access your account faster.</p>
            </div>
            <GoogleSignIn @success="onGoogleSuccess" />
          </div>
          <p v-if="googleError" class="text-sm text-red-600 mt-3">{{ googleError }}</p>
        </div>

        <div class="border-t border-slate-100 pt-4 space-y-4">
          <div class="flex items-center justify-between">
            <p class="text-xs uppercase tracking-wide text-slate-500">Email & Password</p>
            <button class="btn-ghost text-xs" type="button" @click="isRegister = !isRegister">
              {{ isRegister ? 'Use existing account' : 'Create an account' }}
            </button>
          </div>

          <div class="space-y-3">
            <div v-if="isRegister" class="space-y-1">
              <label class="block text-xs text-slate-600">Display name</label>
              <input v-model="displayName" class="input" placeholder="Your name" />
            </div>
            <div class="space-y-1">
              <label class="block text-xs text-slate-600">Email address</label>
              <input v-model="email" class="input" placeholder="name@example.com" type="email" />
            </div>
            <div class="space-y-1">
              <label class="block text-xs text-slate-600">Password</label>
              <input v-model="password" class="input" placeholder="••••••••" type="password" />
            </div>
            <button class="btn-primary w-full" :disabled="passwordLoading || !email || !password || (isRegister && !displayName)" @click="submitPassword">
              <span v-if="passwordLoading">{{ isRegister ? 'Creating account...' : 'Signing in...' }}</span>
              <span v-else>{{ isRegister ? 'Create account' : 'Sign in with password' }}</span>
            </button>
            <RouterLink to="/reset-password" class="text-xs text-brand-700 font-medium inline-flex items-center justify-center w-full">
              Forgot password?
            </RouterLink>
            <p v-if="passwordError" class="text-xs text-red-600">{{ passwordError }}</p>
            <p v-if="passwordNote" class="text-xs text-emerald-700">{{ passwordNote }}</p>
          </div>
        </div>

        <div class="border-t border-slate-100 pt-4">
          <p class="text-xs uppercase tracking-wide text-slate-500 mb-2">SMS OTP</p>
          <div class="space-y-3">
            <div class="space-y-1">
              <label class="block text-xs text-slate-600">Phone number</label>
              <input v-model="smsPhone" class="input" placeholder="+94..." />
            </div>
            <div class="space-y-1">
              <label class="block text-xs text-slate-600">Enter code</label>
              <input v-model="smsOtp" class="input" placeholder="6-digit code" />
            </div>
            <button class="btn-primary w-full" :disabled="smsBusy || !smsPhone || (!smsOtp && !canResendSms)" @click="handleSmsAction">
              <span v-if="smsBusy">Working...</span>
              <span v-else-if="smsOtp">Verify & sign in</span>
              <span v-else-if="canResendSms">Send code</span>
              <span v-else>Resend available in {{ smsCountdown }}s</span>
            </button>
            <p v-if="otpSent" class="text-xs text-green-700">Code sent. Enter it above to continue.</p>
            <p v-if="smsError" class="text-xs text-red-600">{{ smsError }}</p>
          </div>
        </div>

        <p class="text-xs text-slate-500">
          By continuing you agree to the Terms and Privacy Policy (placeholders).
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import GoogleSignIn from '../components/GoogleSignIn.vue'
import { brandConfig, hasImage } from '../lib/branding'

const auth = useAuthStore()
const router = useRouter()
const smsPhone = ref('')
const smsOtp = ref('')
const otpSent = ref(false)
const smsError = ref('')
const sendingOtp = ref(false)
const verifying = ref(false)
const smsCountdown = ref(0)
const resendAvailableAt = ref<number | null>(null)
let resendTimer: number | null = null
const email = ref('')
const password = ref('')
const displayName = ref('')
const isRegister = ref(false)
const passwordLoading = ref(false)
const passwordError = ref('')
const passwordNote = ref('')
const googleError = ref('')
const normalizedPhone = computed(() => smsPhone.value.trim())
const isValidSriLankaPhone = computed(() => isLkPhone(normalizedPhone.value))
const brandLoginIllustration = computed(() => (hasImage(brandConfig.loginIllustrationUrl) ? brandConfig.loginIllustrationUrl : ''))

function redirectHome() {
  router.replace('/')
}

async function onGoogleSuccess(idToken: string) {
  googleError.value = ''
  try {
    await auth.googleLogin(idToken)
    redirectHome()
  } catch (err: any) {
    googleError.value = err?.response?.data?.message ?? 'Unable to sign in with Google.'
  }
}

async function submitPassword() {
  passwordError.value = ''
  passwordNote.value = ''
  passwordLoading.value = true
  try {
    if (isRegister.value) {
      await auth.passwordRegister(email.value.trim(), password.value, displayName.value.trim())
      passwordNote.value = 'Account created. Please verify your email.'
    } else {
      await auth.passwordLogin(email.value.trim(), password.value)
    }
    redirectHome()
  } catch (err: any) {
    passwordError.value = err?.response?.data?.message ?? 'Unable to sign in.'
  } finally {
    passwordLoading.value = false
  }
}

async function sendSmsOtp() {
  smsError.value = ''
  if (!isValidSriLankaPhone.value) {
    smsError.value = 'Enter a valid Sri Lankan phone (07XXXXXXXX or +94XXXXXXXXX).'
    return
  }
  sendingOtp.value = true
  try {
    await auth.requestSmsOtp(normalizedPhone.value)
    otpSent.value = true
  } catch (err: any) {
    smsError.value = err?.response?.data?.message ?? 'Failed to send code. Try again.'
  } finally {
    sendingOtp.value = false
  }
}

async function verifySms() {
  smsError.value = ''
  if (!isValidSriLankaPhone.value) {
    smsError.value = 'Enter a valid Sri Lankan phone (07XXXXXXXX or +94XXXXXXXXX).'
    return
  }
  verifying.value = true
  try {
    await auth.verifySmsOtp(normalizedPhone.value, smsOtp.value.trim())
    redirectHome()
  } catch (err: any) {
    smsError.value = err?.response?.data?.message ?? 'Invalid code.'
  } finally {
    verifying.value = false
  }
}

const smsBusy = computed(() => sendingOtp.value || verifying.value)

const canResendSms = computed(() => {
  return !resendAvailableAt.value || Date.now() >= resendAvailableAt.value
})

function startResendTimer(seconds: number) {
  resendAvailableAt.value = Date.now() + seconds * 1000
  if (resendTimer) window.clearInterval(resendTimer)
  smsCountdown.value = seconds
  resendTimer = window.setInterval(() => {
    if (!resendAvailableAt.value) return
    const remaining = Math.max(0, Math.ceil((resendAvailableAt.value - Date.now()) / 1000))
    smsCountdown.value = remaining
    if (remaining <= 0 && resendTimer) {
      window.clearInterval(resendTimer)
      resendTimer = null
    }
  }, 500)
}

async function handleSmsAction() {
  if (!smsOtp.value) {
    await sendSmsOtp()
    if (!smsError.value) startResendTimer(45)
    return
  }
  await verifySms()
}

onMounted(() => {
  if (auth.isAuthenticated) redirectHome()
})

watch(() => auth.isAuthenticated, (isAuthed) => {
  if (isAuthed) redirectHome()
})

onUnmounted(() => {
  if (resendTimer) window.clearInterval(resendTimer)
})

function isLkPhone(value: string) {
  return /^(?:\+94|0)7\d{8}$/.test(value)
}
</script>
