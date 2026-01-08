<template>
  <div class="mx-auto max-w-xl px-4 py-10">
    <div class="flex items-center justify-between gap-4">
      <div>
        <div class="text-2xl font-semibold">Reset password</div>
        <p class="text-sm text-slate-600 mt-1">
          {{ token ? 'Set a new password for your account.' : 'We will email you a reset link.' }}
        </p>
      </div>
      <RouterLink to="/sign-in" class="btn-ghost">Back to sign in</RouterLink>
    </div>

    <div class="card p-6 mt-6 space-y-4">
      <div v-if="!token" class="space-y-3">
        <div>
          <label class="text-xs text-slate-600">Email address</label>
          <input v-model="email" class="input mt-1" type="email" placeholder="name@example.com" />
        </div>
        <button class="btn-primary w-full" :disabled="loading || !email" @click="requestReset">
          <span v-if="loading">Sending…</span>
          <span v-else>Send reset link</span>
        </button>
      </div>

      <div v-else class="space-y-3">
        <div>
          <label class="text-xs text-slate-600">New password</label>
          <input v-model="password" class="input mt-1" type="password" placeholder="At least 8 characters" />
        </div>
        <div>
          <label class="text-xs text-slate-600">Confirm password</label>
          <input v-model="confirm" class="input mt-1" type="password" placeholder="Repeat password" />
        </div>
        <button class="btn-primary w-full" :disabled="loading || !password || !confirm" @click="confirmReset">
          <span v-if="loading">Updating…</span>
          <span v-else>Update password</span>
        </button>
      </div>

      <p v-if="message" class="text-sm text-emerald-700">{{ message }}</p>
      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const confirm = ref('')
const loading = ref(false)
const error = ref('')
const message = ref('')

const token = computed(() => (route.query.token ? String(route.query.token) : ''))

async function requestReset() {
  error.value = ''
  message.value = ''
  loading.value = true
  try {
    await auth.requestPasswordReset(email.value.trim())
    message.value = 'If that email exists, a reset link has been sent.'
  } catch (err: any) {
    error.value = err?.response?.data?.message ?? 'Failed to send reset email.'
  } finally {
    loading.value = false
  }
}

async function confirmReset() {
  error.value = ''
  message.value = ''
  if (password.value !== confirm.value) {
    error.value = 'Passwords do not match.'
    return
  }
  loading.value = true
  try {
    await auth.confirmPasswordReset(token.value, password.value)
    message.value = 'Password updated. Please sign in again.'
    setTimeout(() => router.replace('/sign-in'), 1200)
  } catch (err: any) {
    error.value = err?.response?.data?.message ?? 'Failed to reset password.'
  } finally {
    loading.value = false
  }
}
</script>
