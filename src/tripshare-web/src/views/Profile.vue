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
              <div class="font-semibold">Profile details</div>
              <div class="text-sm text-slate-600 mt-1">Update your public profile and phone number.</div>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-4">
            <div>
              <label class="text-xs text-slate-600">Display name</label>
              <input v-model="profileForm.displayName" class="input mt-1" placeholder="Your name" />
            </div>
            <div>
              <label class="text-xs text-slate-600">Profile photo</label>
              <input type="file" accept="image/*" class="input mt-1" @change="onPhotoSelected" />
              <p class="text-xs text-slate-500 mt-1">Upload a square image for best results.</p>
            </div>
            <div>
              <label class="text-xs text-slate-600">Phone number</label>
              <input v-model="profileForm.phoneNumber" class="input mt-1" placeholder="+94XXXXXXXXX" />
            </div>
            <div>
              <label class="text-xs text-slate-600">Photo URL (optional)</label>
              <input v-model="profileForm.photoUrl" class="input mt-1" placeholder="https://..." />
            </div>
          </div>

          <div class="mt-4 flex items-center gap-3">
            <button class="btn-primary" :disabled="profileSaving" @click="updateProfile">
              <span v-if="profileSaving">Saving…</span>
              <span v-else>Save changes</span>
            </button>
            <p v-if="profileMsg" class="text-sm text-emerald-700">{{ profileMsg }}</p>
            <p v-if="profileErr" class="text-sm text-red-600">{{ profileErr }}</p>
          </div>
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
            Trips can only be created and booked after verifying your email and phone number. In development mode, the API writes the email to
            <span class="font-mono">App_Data/dev-emails</span>.
          </p>

          <p v-if="msg" class="text-sm text-brand-700 mt-3">{{ msg }}</p>
        </div>

        <div class="mt-5 rounded-2xl border border-slate-100 p-5 bg-slate-50">
          <div class="flex items-center justify-between">
            <div>
              <div class="font-semibold">Phone verification</div>
              <div class="text-sm text-slate-600 mt-1">
                Status:
                <span v-if="auth.me?.phoneVerified" class="badge">Verified</span>
                <span v-else class="badge bg-amber-50 text-amber-800 border-amber-100">Not verified</span>
              </div>
            </div>
          </div>

          <div v-if="!auth.me?.phoneVerified" class="mt-4 space-y-3">
            <div>
              <label class="text-xs text-slate-600">Phone number</label>
              <input v-model="phoneVerification.phone" class="input mt-1" placeholder="+94XXXXXXXXX" />
            </div>
            <div>
              <label class="text-xs text-slate-600">Verification code</label>
              <input v-model="phoneVerification.otp" class="input mt-1" placeholder="6-digit code" />
            </div>
            <button class="btn-primary w-full" :disabled="phoneVerification.loading || !phoneVerification.phone || (!canResendPhoneCode && !phoneVerification.otp)" @click="submitPhoneVerification">
              <span v-if="phoneVerification.loading">Working…</span>
              <span v-else-if="phoneVerification.otp">Verify phone number</span>
              <span v-else-if="canResendPhoneCode">Send verification code</span>
              <span v-else>Resend available in {{ resendCountdown }}s</span>
            </button>
            <p class="text-xs text-slate-500">Enter your number, then tap to send or verify the code.</p>
            <p v-if="phoneVerification.error" class="text-sm text-red-600">{{ phoneVerification.error }}</p>
            <p v-if="phoneVerification.success" class="text-sm text-emerald-700">{{ phoneVerification.success }}</p>
          </div>
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
              <select v-model="idvForm.documentType" class="input mt-1">
                <option value="">Select a document</option>
                <option value="DriverLicense">Driver license</option>
                <option value="NIC">National ID</option>
                <option value="Passport">Passport</option>
              </select>
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

        <div class="mt-5 rounded-2xl border border-slate-100 p-5 bg-slate-50">
          <div class="flex items-center justify-between">
            <div>
              <div class="font-semibold">Account data</div>
              <div class="text-sm text-slate-600 mt-1">Export or delete your account.</div>
            </div>
          </div>

          <div class="mt-4 flex flex-wrap gap-3">
            <button class="btn-outline" :disabled="exporting" @click="exportData">
              <span v-if="exporting">Preparing…</span>
              <span v-else>Export data (JSON)</span>
            </button>
            <button class="btn-ghost text-red-700" :disabled="deleting" @click="deleteAccount">
              <span v-if="deleting">Deleting…</span>
              <span v-else>Delete account</span>
            </button>
          </div>
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
import { computed, onMounted, onUnmounted, ref } from 'vue'
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
const profileForm = ref({ displayName: '', photoUrl: '', phoneNumber: '' })
const profileSaving = ref(false)
const profileMsg = ref('')
const profileErr = ref('')
const phoneVerification = ref({
  phone: '',
  otp: '',
  loading: false,
  error: '',
  success: ''
})
const resendAvailableAt = ref<number | null>(null)
const resendCountdown = ref(0)
let resendTimer: number | null = null
const exporting = ref(false)
const deleting = ref(false)

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

function syncProfileForm() {
  profileForm.value.displayName = auth.me?.displayName ?? ''
  profileForm.value.photoUrl = auth.me?.photoUrl ?? ''
  profileForm.value.phoneNumber = auth.me?.phoneNumber ?? ''
  if (!phoneVerification.value.phone) {
    phoneVerification.value.phone = auth.me?.phoneNumber ?? ''
  }
}

function onPhotoSelected(event: Event) {
  const input = event.target as HTMLInputElement | null
  const file = input?.files?.[0] ?? null
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    profileForm.value.photoUrl = String(reader.result)
  }
  reader.readAsDataURL(file)
}

async function updateProfile() {
  profileSaving.value = true
  profileMsg.value = ''
  profileErr.value = ''
  try {
    await http.put('/users/me/profile', {
      displayName: profileForm.value.displayName.trim() || auth.me?.email || 'User',
      photoUrl: profileForm.value.photoUrl?.trim() || null,
      phoneNumber: profileForm.value.phoneNumber?.trim() || null
    })
    await auth.loadMe()
    syncProfileForm()
    profileMsg.value = 'Profile updated.'
  } catch (e: any) {
    profileErr.value = e?.response?.data?.message ?? 'Failed to update profile.'
  } finally {
    profileSaving.value = false
  }
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

async function exportData() {
  exporting.value = true
  try {
    const resp = await http.get('/users/me/export', { responseType: 'blob' })
    const blob = resp.data as Blob
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `hoptrip-account-${auth.me?.id ?? 'export'}.json`
    a.click()
    URL.revokeObjectURL(url)
  } finally {
    exporting.value = false
  }
}

async function deleteAccount() {
  if (!confirm('This will permanently delete your account and data. Continue?')) return
  deleting.value = true
  try {
    await http.delete('/users/me')
    auth.logout()
    router.push('/')
  } catch (e: any) {
    alert(e?.response?.data?.message ?? 'Failed to delete account.')
  } finally {
    deleting.value = false
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

function startResendTimer(seconds: number) {
  resendAvailableAt.value = Date.now() + seconds * 1000
  if (resendTimer) window.clearInterval(resendTimer)
  resendCountdown.value = seconds
  resendTimer = window.setInterval(() => {
    if (!resendAvailableAt.value) return
    const remaining = Math.max(0, Math.ceil((resendAvailableAt.value - Date.now()) / 1000))
    resendCountdown.value = remaining
    if (remaining <= 0 && resendTimer) {
      window.clearInterval(resendTimer)
      resendTimer = null
    }
  }, 500)
}

const canResendPhoneCode = computed(() => {
  return !resendAvailableAt.value || Date.now() >= resendAvailableAt.value
})

async function submitPhoneVerification() {
  phoneVerification.value.error = ''
  phoneVerification.value.success = ''
  phoneVerification.value.loading = true
  try {
    if (!phoneVerification.value.otp) {
      await http.post('/users/me/phone/request', { phoneNumber: phoneVerification.value.phone.trim() })
      phoneVerification.value.success = 'Code sent. Check your phone.'
      startResendTimer(45)
    } else {
      await http.post('/users/me/phone/verify', {
        phoneNumber: phoneVerification.value.phone.trim(),
        otp: phoneVerification.value.otp.trim()
      })
      phoneVerification.value.success = 'Phone number verified.'
      phoneVerification.value.otp = ''
      await auth.refresh()
    }
  } catch (err: any) {
    phoneVerification.value.error = err?.response?.data?.message ?? 'Unable to verify phone.'
  } finally {
    phoneVerification.value.loading = false
  }
}

onMounted(() => {
  syncProfileForm()
  void loadIdv()
})

onUnmounted(() => {
  if (resendTimer) window.clearInterval(resendTimer)
})
</script>
