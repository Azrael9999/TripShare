<template>
  <div>
    <div ref="btnEl"></div>
    <p v-if="error" class="text-sm text-red-600 mt-2">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'

const emit = defineEmits<{ (e: 'success', idToken: string): void }>()
const btnEl = ref<HTMLDivElement | null>(null)
const error = ref('')

declare global {
  interface Window { google?: any }
}

onMounted(() => {
  const clientId = (import.meta as any).env.VITE_GOOGLE_CLIENT_ID ?? '426254863138-thl2o6eqo7uq3km80kg1nrcv4n27c890.apps.googleusercontent.com'
  if (!clientId) {
    error.value = 'Google Client ID is missing. Set VITE_GOOGLE_CLIENT_ID in .env.'
    return
  }
  if (!btnEl.value) return

  const google = window.google
  if (!google?.accounts?.id) {
    error.value = 'Google Identity Services is not available yet. Refresh the page.'
    return
  }

  google.accounts.id.initialize({
    client_id: clientId,
    callback: (response: any) => {
      if (!response?.credential) {
        error.value = 'Google sign-in did not return a credential.'
        return
      }
      emit('success', response.credential)
    }
  })

  google.accounts.id.renderButton(btnEl.value, {
    theme: 'outline',
    size: 'large',
    width: 320,
    text: 'continue_with'
  })
})
</script>
