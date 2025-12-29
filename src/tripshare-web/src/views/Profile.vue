<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-4">
      <div class="card p-6">
        <div class="flex items-center gap-4">
          <img v-if="auth.me?.photoUrl" :src="auth.me.photoUrl" class="h-16 w-16 rounded-full" />
          <div class="flex-1">
            <div class="text-2xl font-semibold">{{ auth.me?.displayName }}</div>
            <div class="text-sm text-slate-600 mt-1">{{ auth.me?.email }}</div>
            <div class="text-sm text-slate-600 mt-1">
              Rating: <span class="font-semibold">{{ (auth.me?.ratingAverage ?? 0).toFixed(1) }}</span>
            </div>
          </div>
          <button class="btn-ghost" @click="logout">Sign out</button>
        </div>

        <div class="mt-5 rounded-2xl border border-slate-100 p-5 bg-slate-50">
          <div class="flex items-center justify-between">
            <div>
              <div class="font-semibold">Email verification</div>
              <div class="text-sm text-slate-600 mt-1">
                Status:
                <span v-if="auth.me?.emailVerified" class="badge">Verified</span>
                <span v-else class="badge bg-amber-50 text-amber-800 border-amber-100">Not verified</span>
              </div>
            </div>

            <button v-if="!auth.me?.emailVerified" class="btn-primary" :disabled="sending" @click="resend">
              <span v-if="sending">Sending…</span>
              <span v-else>Resend email</span>
            </button>
          </div>

          <p v-if="!auth.me?.emailVerified" class="text-sm text-slate-600 mt-3">
            Trips can only be created and booked after verification. In development mode, the API writes the email to
            <span class="font-mono">TripShare.Api/App_Data/dev-emails</span>.
          </p>

          <p v-if="msg" class="text-sm text-brand-700 mt-3">{{ msg }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-4">
      <AdSlot />
      <div class="card p-6">
        <div class="text-lg font-semibold">Safety</div>
        <p class="text-sm text-slate-600 mt-1">Always meet in a public place. Share trip details with someone you trust.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import AdSlot from '../components/AdSlot.vue'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const sending = ref(false)
const msg = ref('')

async function resend() {
  sending.value = true
  msg.value = ''
  try {
    await auth.resendVerification()
    msg.value = 'Verification email sent.'
  } finally {
    sending.value = false
  }
}

function logout() {
  auth.logout()
  router.push('/')
}
</script>
