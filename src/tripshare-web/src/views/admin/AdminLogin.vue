<template>
  <div class="mx-auto max-w-lg px-4 py-12">
    <div class="card p-6 space-y-6">
      <div>
        <div class="text-2xl font-semibold">Admin sign in</div>
        <p class="text-sm text-slate-600 mt-1">This entry point is reserved for approved administrators.</p>
      </div>

      <div class="space-y-3">
        <div class="space-y-1">
          <label class="block text-xs text-slate-600">Email address</label>
          <input v-model="email" class="input" placeholder="admin@example.com" type="email" />
        </div>
        <div class="space-y-1">
          <label class="block text-xs text-slate-600">Password</label>
          <input v-model="password" class="input" placeholder="••••••••" type="password" />
        </div>
        <button class="btn-primary w-full" :disabled="busy || !email || !password" @click="submit">
          <span v-if="busy">Signing in...</span>
          <span v-else>Sign in</span>
        </button>
        <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const email = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')

async function submit() {
  error.value = ''
  busy.value = true
  try {
    await auth.passwordLogin(email.value.trim(), password.value)
    if (auth.me?.role !== 'admin' && auth.me?.role !== 'superadmin') {
      auth.logout()
      error.value = 'This login is restricted to approved admins.'
      return
    }
    router.replace('/admin')
  } catch (err: any) {
    error.value = err?.response?.data?.message ?? 'Unable to sign in.'
  } finally {
    busy.value = false
  }
}
</script>
