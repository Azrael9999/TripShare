<template>
  <div class="mx-auto max-w-xl">
    <div class="card p-8 text-center">
      <div class="text-2xl font-semibold">Email verification</div>
      <p class="text-slate-600 mt-2">{{ status }}</p>

      <div v-if="done" class="mt-6">
        <RouterLink to="/profile" class="btn-primary">Go to profile</RouterLink>
      </div>
    </div>

    <div class="mt-5">
      <AdSlot />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { http } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'

const route = useRoute()
const status = ref('Verifying…')
const done = ref(false)

async function run() {
  const token = String(route.query.token ?? '')
  if (!token) {
    status.value = 'Missing token.'
    done.value = true
    return
  }

  try {
    await http.post(`/users/verify-email?token=${encodeURIComponent(token)}`)
    status.value = 'Verified. You can now create and book trips. Please refresh your session if needed.'
  } catch (e: any) {
    status.value = e?.response?.data?.message ?? 'Verification failed.'
  } finally {
    done.value = true
  }
}
run()
</script>
