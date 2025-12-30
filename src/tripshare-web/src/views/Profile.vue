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

        <div class="mt-5 rounded-2xl border border-slate-100 p-5 bg-slate-50">
          <div class="flex items-center justify-between">
            <div>
              <div class="font-semibold">Identity verification</div>
              <div class="text-sm text-slate-600 mt-1 flex items-center gap-2">
                <span>Status:</span>
                <span v-if="idvStatus === 'Approved'" class="badge bg-emerald-50 text-emerald-700 border-emerald-100">Approved</span>
                <span v-else-if="idvStatus === 'Pending'" class="badge bg-amber-50 text-amber-800 border-amber-100">Pending</span>
                <span v-else-if="idvStatus === 'Rejected'" class="badge bg-red-50 text-red-700 border-red-100">Rejected</span>
                <span v-else class="badge bg-slate-100 text-slate-600 border-slate-200">Not submitted</span>
              </div>
            </div>
            <button class="btn-outline" :disabled="idvLoading" @click="loadIdv">Refresh</button>
          </div>

          <p class="text-sm text-slate-600 mt-3">
            Drivers who complete identity checks get a verified badge in search and trip details. Submit a document reference
            so admins can approve you.
          </p>

          <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="text-xs text-slate-600">Document type</label>
              <input v-model="idvForm.documentType" class="input mt-1" placeholder="Driver license / NIC / Passport" />
            </div>
            <div>
              <label class="text-xs text-slate-600">Document reference</label>
              <input v-model="idvForm.documentReference" class="input mt-1" placeholder="Reference / last 4 / URL" />
            </div>
            <div>
              <label class="text-xs text-slate-600">KYC provider (optional)</label>
              <input v-model="idvForm.kycProvider" class="input mt-1" placeholder="e.g., Onfido" />
            </div>
            <div>
              <label class="text-xs text-slate-600">Provider reference (optional)</label>
              <input v-model="idvForm.kycReference" class="input mt-1" placeholder="Provider result ID" />
            </div>
          </div>

          <div class="mt-4 flex items-center gap-3">
            <button class="btn-primary" :disabled="idvSubmitting" @click="submitIdv">
              <span v-if="idvSubmitting">Submitting…</span>
              <span v-else>{{ idvStatus === 'Pending' ? 'Resubmit' : 'Submit' }}</span>
            </button>
            <p class="text-xs text-slate-600">
              We only use this to protect riders. Admins review and flag trusted drivers.
            </p>
          </div>

          <p v-if="idvMsg" class="text-sm text-emerald-700 mt-3">{{ idvMsg }}</p>
          <p v-if="idvErr" class="text-sm text-red-600 mt-3">{{ idvErr }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-4">
      <AdSlot name="profile-sidebar" />
      <div class="card p-6">
        <div class="text-lg font-semibold">Safety</div>
        <p class="text-sm text-slate-600 mt-1">Always meet in a public place. Share trip details with someone you trust.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AdSlot from '../components/AdSlot.vue'
import { useAuthStore } from '../stores/auth'
import { http } from '../lib/api'

const auth = useAuthStore()
const router = useRouter()
const sending = ref(false)
const msg = ref('')
const idvMsg = ref('')
const idvErr = ref('')
const idvLoading = ref(false)
const idvSubmitting = ref(false)
const idvStatus = ref<string>('')
const idvForm = ref({
  documentType: '',
  documentReference: '',
  kycProvider: '',
  kycReference: ''
})

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

async function loadIdv() {
  idvLoading.value = true
  idvErr.value = ''
  try {
    const resp = await http.get('/users/me/identity-verification')
    const data = resp.data
    if (data) {
      idvStatus.value = data.status
      idvForm.value.documentType = data.documentType ?? ''
      idvForm.value.documentReference = data.documentReference ?? ''
      idvForm.value.kycProvider = data.kycProvider ?? ''
      idvForm.value.kycReference = data.kycReference ?? ''
    } else {
      idvStatus.value = ''
    }
  } catch (e: any) {
    idvErr.value = e?.response?.data?.message ?? 'Unable to load verification status.'
  } finally {
    idvLoading.value = false
  }
}

async function submitIdv() {
  idvSubmitting.value = true
  idvMsg.value = ''
  idvErr.value = ''
  try {
    await http.post('/users/me/identity-verification', { ...idvForm.value })
    idvMsg.value = 'Submitted. An admin will review your documents.'
    await loadIdv()
    await auth.loadMe()
  } catch (e: any) {
    idvErr.value = e?.response?.data?.message ?? 'Submission failed.'
  } finally {
    idvSubmitting.value = false
  }
}

onMounted(() => {
  void loadIdv()
})
</script>
