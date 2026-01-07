<template>
  <div class="mx-auto max-w-5xl px-4 py-10">
    <div class="flex items-center justify-between gap-4">
      <div>
        <div class="text-2xl font-semibold">Sign in</div>
        <p class="text-sm text-slate-600 mt-1">
          Trips can be created and booked only after email verification.
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
        <div>
          <p class="text-xs uppercase tracking-wide text-slate-500 mb-2">Google</p>
          <GoogleSignIn @success="onGoogleSuccess" />
        </div>

        <div class="border-t border-slate-100 pt-4">
          <p class="text-xs uppercase tracking-wide text-slate-500 mb-2">SMS OTP</p>
          <div class="space-y-3">
            <div class="space-y-1">
              <label class="block text-xs text-slate-600">Phone number</label>
              <input v-model="smsPhone" class="input" placeholder="+94..." />
            </div>
            <div class="flex items-center gap-3">
              <button class="btn-secondary" :disabled="sendingOtp || !smsPhone" @click="sendSmsOtp">
                <span v-if="sendingOtp">Sending...</span>
                <span v-else>Send code</span>
              </button>
              <span v-if="otpSent" class="text-xs text-green-700">Code sent</span>
            </div>
            <div class="space-y-1">
              <label class="block text-xs text-slate-600">Enter code</label>
              <input v-model="smsOtp" class="input" placeholder="6-digit code" />
            </div>
            <button class="btn-primary w-full" :disabled="verifying || !smsOtp" @click="verifySms">
              <span v-if="verifying">Verifying...</span>
              <span v-else>Sign in with SMS</span>
            </button>
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
import { computed, onMounted, ref, watch } from 'vue'
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
const normalizedPhone = computed(() => smsPhone.value.trim())
const isValidSriLankaPhone = computed(() => isLkPhone(normalizedPhone.value))
const brandLoginIllustration = computed(() => (hasImage(brandConfig.loginIllustrationUrl) ? brandConfig.loginIllustrationUrl : ''))

function redirectHome() {
  router.replace('/')
}

async function onGoogleSuccess(idToken: string) {
  await auth.googleLogin(idToken)
  redirectHome()
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

onMounted(() => {
  if (auth.isAuthenticated) redirectHome()
})

watch(() => auth.isAuthenticated, (isAuthed) => {
  if (isAuthed) redirectHome()
})

function isLkPhone(value: string) {
  return /^(?:\+94|0)7\d{8}$/.test(value)
}
</script>
