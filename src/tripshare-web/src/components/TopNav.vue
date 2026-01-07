<template>
  <header class="sticky top-0 z-30 bg-white/80 backdrop-blur border-b border-slate-100">
    <div class="mx-auto max-w-6xl px-4 py-3 flex items-center justify-between gap-3">
      <RouterLink to="/" class="flex items-center gap-3">
        <div class="h-10 w-10 rounded-2xl bg-gradient-to-br from-brand-600 to-brand-800 shadow-soft flex items-center justify-center overflow-hidden">
          <img v-if="brandLogo" :src="brandLogo" alt="HopTrip logo" class="h-full w-full object-cover" />
          <span v-else class="text-white font-semibold text-sm">HT</span>
        </div>
        <div class="leading-tight">
          <div class="font-semibold">HopTrip</div>
          <div class="text-xs text-slate-500">Carpool, simplified</div>
        </div>
      </RouterLink>

      <nav class="hidden md:flex items-center gap-1">
        <RouterLink to="/" class="btn-ghost">Explore</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified" to="/create" class="btn-ghost">Create</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/my-trips" class="btn-ghost">My Trips</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified" to="/bookings" class="btn-ghost">Bookings</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/notifications" class="btn-ghost">
          <span class="inline-flex items-center gap-2">
            <BellIcon class="h-5 w-5"/>
            <span>Alerts</span>
            <span v-if="unreadCount>0" class="ml-1 inline-flex items-center justify-center h-5 min-w-5 px-1 rounded-full bg-brand-600 text-white text-xs">{{ unreadCount }}</span>
          </span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/messages" class="btn-ghost">Messages</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/profile" class="btn-ghost">Profile</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/safety" class="btn-ghost">Safety</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.role==='admin'" to="/admin" class="btn-ghost">Admin</RouterLink>
      </nav>

      <div class="flex items-center gap-2">
        <button v-if="!auth.isAuthenticated" class="btn-primary" @click="openLogin=true">
          <ArrowRightOnRectangleIcon class="h-5 w-5 mr-2"/> Sign in
        </button>
        <RouterLink v-else to="/profile" class="btn-outline flex items-center gap-2">
          <img v-if="auth.me?.photoUrl" :src="auth.me?.photoUrl" class="h-7 w-7 rounded-full object-cover" />
          <UserCircleIcon v-else class="h-6 w-6 text-slate-500" />
          <span class="hidden sm:inline">{{ auth.me?.displayName }}</span>
        </RouterLink>
      </div>
    </div>

    <!-- Mobile nav -->
    <div class="md:hidden border-t border-slate-100 bg-white">
      <div class="mx-auto max-w-6xl px-4 py-2 flex items-center justify-between text-sm">
        <RouterLink to="/" class="btn-ghost px-3 py-2">Explore</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified" to="/create" class="btn-ghost px-3 py-2">Create</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/notifications" class="btn-ghost px-3 py-2">Alerts</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/messages" class="btn-ghost px-3 py-2">Messages</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/profile" class="btn-ghost px-3 py-2">Profile</RouterLink>
      </div>
    </div>

    <!-- Login modal -->
    <div v-if="openLogin" class="fixed inset-0 z-40 bg-slate-900/60 flex items-center justify-center p-4 sm:p-6 overflow-y-auto soft-modal">
      <div class="card w-full max-w-2xl p-6 max-h-[90dvh] overflow-y-auto">
        <div class="flex items-start justify-between gap-4">
          <div class="flex-1">
            <div class="text-lg font-semibold">Sign in</div>
            <p class="text-sm text-slate-600 mt-1">
              Trips can be created and booked only after email verification.
            </p>
          </div>
          <button class="btn-ghost" @click="openLogin=false"><XMarkIcon class="h-5 w-5"/></button>
        </div>
        <div class="mt-4 grid grid-cols-1 sm:grid-cols-[160px_1fr] gap-4 items-start">
          <div class="hidden sm:block self-start">
            <div class="w-full min-h-[140px] bg-gradient-to-br from-brand-50 to-white rounded-xl flex items-center justify-center border border-slate-100 p-3">
              <img v-if="brandLoginIllustration" :src="brandLoginIllustration" alt="Sign-in illustration" class="max-h-40 object-contain" />
              <div v-else class="text-xs text-slate-500 text-center px-3">Secure sign-in</div>
            </div>
          </div>
          <div class="space-y-4">
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
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import GoogleSignIn from './GoogleSignIn.vue'
import { http } from '../lib/api'
import { brandConfig, hasImage } from '../lib/branding'
import {
  ArrowRightOnRectangleIcon,
  BellIcon,
  UserCircleIcon,
  XMarkIcon
} from '@heroicons/vue/24/outline'

const auth = useAuthStore()
const openLogin = ref(false)
const unreadCount = ref(0)
const smsPhone = ref('')
const smsOtp = ref('')
const otpSent = ref(false)
const smsError = ref('')
const sendingOtp = ref(false)
const verifying = ref(false)
const normalizedPhone = computed(() => smsPhone.value.trim())
const isValidSriLankaPhone = computed(() => isLkPhone(normalizedPhone.value))
const brandLogo = computed(() => (hasImage(brandConfig.logoUrl) ? brandConfig.logoUrl : ''))
const brandLoginIllustration = computed(() => (hasImage(brandConfig.loginIllustrationUrl) ? brandConfig.loginIllustrationUrl : ''))

async function refreshUnread() {
  if (!auth.isAuthenticated) { unreadCount.value = 0; return }
  // If backend doesn't have a count endpoint, approximate by fetching unreadOnly=1
  try {
    const resp = await http.get<any[]>('/notifications', { params: { unreadOnly: true, take: 50 } })
    unreadCount.value = (resp.data ?? []).length
  } catch {
    unreadCount.value = 0
  }
}

async function onGoogleSuccess(idToken: string) {
  await auth.googleLogin(idToken)
  openLogin.value = false
  await refreshUnread()
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
    openLogin.value = false
    await refreshUnread()
  } catch (err: any) {
    smsError.value = err?.response?.data?.message ?? 'Invalid code.'
  } finally {
    verifying.value = false
  }
}

onMounted(() => { refreshUnread() })
watch(() => auth.isAuthenticated, () => { refreshUnread() })

function isLkPhone(value: string) {
  return /^(?:\+94|0)7\d{8}$/.test(value)
}
</script>
