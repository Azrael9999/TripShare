<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-6">
      <div class="card p-5">
        <div class="text-xl font-semibold">Admin dashboard</div>
        <p class="text-sm text-slate-600 mt-1">Overview of the platform health.</p>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div class="card p-5" v-for="c in cards" :key="c.label">
          <div class="text-sm text-slate-600">{{ c.label }}</div>
          <div class="text-3xl font-semibold mt-2">{{ c.value }}</div>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Quick actions</div>
            <p class="text-sm text-slate-600 mt-1">Moderate reports and hide abusive trips.</p>
          </div>
          <RouterLink to="/admin/reports" class="btn-primary">View reports</RouterLink>
        </div>
      </div>

      <div class="card p-5 grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <div class="font-semibold">Identity verification</div>
          <p class="text-sm text-slate-600 mt-1">Review driver documents to unlock verified badges.</p>
          <RouterLink to="/admin/identity" class="btn-outline mt-3 inline-flex items-center">Review submissions</RouterLink>
        </div>
        <div>
          <div class="font-semibold">Ad configuration</div>
          <p class="text-sm text-slate-600 mt-1">Manage slots and client-side frequency caps.</p>
          <RouterLink to="/admin/ads" class="btn-outline mt-3 inline-flex items-center">Configure ads</RouterLink>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="font-semibold">Driver verification gate</div>
            <p class="text-sm text-slate-600 mt-1">
              Require admin-verified drivers before creating trips. Applies platform-wide.
            </p>
          </div>
          <button class="btn-outline" @click="toggleDriverVerification">
            {{ driverVerificationRequired ? 'Disable' : 'Enable' }}
          </button>
        </div>
        <p class="text-sm text-slate-600 mt-3">
          Current state: <span class="badge">{{ driverVerificationRequired ? 'Required' : 'Off' }}</span>
        </p>
        <p v-if="settingsMsg" class="text-sm text-emerald-700 mt-2">{{ settingsMsg }}</p>
        <p v-if="settingsErr" class="text-sm text-red-600 mt-2">{{ settingsErr }}</p>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="font-semibold">Branding assets</div>
            <p class="text-sm text-slate-600 mt-1">Upload logos and banners used across the site.</p>
          </div>
          <button class="btn-primary" :disabled="brandingSaving" @click="saveBranding">
            <span v-if="brandingSaving">Saving…</span>
            <span v-else>Save branding</span>
          </button>
        </div>

        <div class="mt-4 grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Logo</div>
            <img v-if="branding.logoUrl" :src="branding.logoUrl" class="h-12 object-contain rounded-xl border border-slate-100 bg-white p-2" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('logoUrl', $event)" />
          </div>
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Hero banner</div>
            <img v-if="branding.heroImageUrl" :src="branding.heroImageUrl" class="h-20 w-full object-cover rounded-xl border border-slate-100" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('heroImageUrl', $event)" />
          </div>
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Map overlay</div>
            <img v-if="branding.mapOverlayUrl" :src="branding.mapOverlayUrl" class="h-20 w-full object-cover rounded-xl border border-slate-100" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('mapOverlayUrl', $event)" />
          </div>
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Login illustration</div>
            <img v-if="branding.loginIllustrationUrl" :src="branding.loginIllustrationUrl" class="h-20 w-full object-cover rounded-xl border border-slate-100" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('loginIllustrationUrl', $event)" />
          </div>
        </div>

        <p v-if="brandingMsg" class="text-sm text-emerald-700 mt-3">{{ brandingMsg }}</p>
        <p v-if="brandingErr" class="text-sm text-red-600 mt-3">{{ brandingErr }}</p>
      </div>

      <div class="card p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        <div class="space-y-2">
          <div class="font-semibold">Suspend / unsuspend user</div>
          <input v-model="suspendUserId" class="input" placeholder="User GUID" />
          <div class="flex gap-2">
            <button class="btn-outline" :disabled="!isGuid(suspendUserId)" @click="suspend(true)">Suspend</button>
            <button class="btn-ghost" :disabled="!isGuid(suspendUserId)" @click="suspend(false)">Unsuspend</button>
          </div>
        </div>
        <div class="space-y-2">
          <div class="font-semibold">Hide / unhide trip</div>
          <input v-model="hideTripId" class="input" placeholder="Trip GUID" />
          <div class="flex gap-2">
            <button class="btn-outline" :disabled="!isGuid(hideTripId)" @click="hide(true)">Hide</button>
            <button class="btn-ghost" :disabled="!isGuid(hideTripId)" @click="hide(false)">Unhide</button>
          </div>
        </div>
      </div>

      <div class="card p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        <div class="space-y-2">
          <div class="font-semibold">Driver verification</div>
          <input v-model="verifyUserId" class="input" placeholder="User GUID" />
          <textarea v-model="verifyNote" class="textarea" rows="2" placeholder="Optional note"></textarea>
          <div class="flex gap-2">
            <button class="btn-outline" :disabled="!isGuid(verifyUserId)" @click="verifyDriver(true)">Mark verified</button>
            <button class="btn-ghost" :disabled="!isGuid(verifyUserId)" @click="verifyDriver(false)">Unverify</button>
          </div>
          <p v-if="verifyMsg" class="text-sm text-emerald-700">{{ verifyMsg }}</p>
          <p v-if="verifyErr" class="text-sm text-red-600">{{ verifyErr }}</p>
        </div>
        <div>
          <div class="font-semibold mb-2 flex items-center justify-between">
            <span>Safety incidents</span>
            <button class="btn-ghost text-xs" @click="loadIncidents" :disabled="incidentsLoading">Refresh</button>
          </div>
          <div v-if="incidentsLoading" class="text-slate-600">Loading…</div>
          <div v-else-if="incidents.length===0" class="text-slate-600">No incidents.</div>
          <div v-else class="space-y-2 max-h-64 overflow-y-auto">
            <div v-for="inc in incidents" :key="inc.id" class="rounded-xl border border-slate-100 p-3 bg-white">
              <div class="flex items-center justify-between">
                <div class="flex flex-wrap gap-2 items-center">
                  <span class="badge">{{ inc.status }}</span>
                  <span class="chip">{{ inc.type }}</span>
                  <span class="text-xs text-slate-500">{{ inc.createdAt }}</span>
                </div>
                <div class="flex gap-1">
                  <button class="btn-outline text-xs" @click="resolveIncident(inc.id, 'Resolved')">Resolve</button>
                  <button class="btn-ghost text-xs" @click="resolveIncident(inc.id, 'Escalated')">Escalate</button>
                </div>
              </div>
              <div class="text-sm text-slate-700 mt-1">{{ inc.summary }}</div>
              <div class="text-xs text-slate-500 mt-1">Trip: {{ inc.tripId || '-' }} · Booking: {{ inc.bookingId || '-' }}</div>
            </div>
          </div>
          <p v-if="incidentsErr" class="text-sm text-red-600 mt-2">{{ incidentsErr }}</p>
        </div>
      </div>

      <p v-if="adminMsg" class="text-sm text-emerald-700">{{ adminMsg }}</p>
      <p v-if="adminErr" class="text-sm text-red-600">{{ adminErr }}</p>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot name="admin-sidebar" />
      <div class="card p-5">
        <div class="font-semibold">Note</div>
        <p class="text-sm text-slate-600 mt-2">Admin endpoints are protected by role and audited in logs.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { http } from '../../lib/api'
import { applyBrandingConfig, type BrandingConfig } from '../../lib/branding'
import AdSlot from '../../components/AdSlot.vue'

const metrics = ref<any|null>(null)
const driverVerificationRequired = ref(false)
const settingsMsg = ref('')
const settingsErr = ref('')
const adminMsg = ref('')
const adminErr = ref('')
const suspendUserId = ref('')
const hideTripId = ref('')
const verifyUserId = ref('')
const verifyNote = ref('')
const verifyMsg = ref('')
const verifyErr = ref('')
const incidents = ref<any[]>([])
const incidentsLoading = ref(false)
const incidentsErr = ref('')
const branding = ref<BrandingConfig>({})
const brandingSaving = ref(false)
const brandingMsg = ref('')
const brandingErr = ref('')

const cards = computed(() => {
  const m = metrics.value ?? {}
  return [
    { label: 'Users', value: m.usersTotal ?? 0 },
    { label: 'Trips', value: m.tripsTotal ?? 0 },
    { label: 'Bookings', value: m.bookingsTotal ?? 0 },
    { label: 'Reports (open)', value: m.reportsOpen ?? 0 }
  ]
})

async function load() {
  const [metricsResp, settingsResp] = await Promise.all([
    http.get('/admin/metrics'),
    http.get('/admin/settings')
  ])
  metrics.value = metricsResp.data
  driverVerificationRequired.value = !!settingsResp.data?.driverVerificationRequired
  await loadBranding()
  await loadIncidents()
}
load()

async function toggleDriverVerification() {
  settingsMsg.value = ''
  settingsErr.value = ''
  try {
    const newVal = !driverVerificationRequired.value
    await http.post('/admin/settings/driver-verification', newVal)
    driverVerificationRequired.value = newVal
    settingsMsg.value = `Driver verification requirement ${newVal ? 'enabled' : 'disabled'}.`
  } catch (e:any) {
    settingsErr.value = e?.response?.data?.message ?? 'Failed to update setting.'
  }
}

async function loadBranding() {
  try {
    const resp = await http.get<BrandingConfig | null>('/admin/branding')
    branding.value = resp.data ?? {}
  } catch {
    branding.value = {}
  }
}

function onBrandFile(key: keyof BrandingConfig, event: Event) {
  const input = event.target as HTMLInputElement | null
  const file = input?.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    branding.value = { ...branding.value, [key]: String(reader.result) }
  }
  reader.readAsDataURL(file)
}

async function saveBranding() {
  brandingMsg.value = ''
  brandingErr.value = ''
  brandingSaving.value = true
  try {
    await http.post('/admin/branding', branding.value)
    applyBrandingConfig(branding.value)
    brandingMsg.value = 'Branding updated.'
  } catch (e:any) {
    brandingErr.value = e?.response?.data?.message ?? 'Failed to update branding.'
  } finally {
    brandingSaving.value = false
  }
}

async function suspend(suspendUser:boolean) {
  adminMsg.value = ''
  adminErr.value = ''
  if (!isGuid(suspendUserId.value)) {
    adminErr.value = 'Enter a valid user ID (GUID).'
    return
  }
  try {
    await http.post(`/admin/users/${suspendUserId.value}/suspend`, { suspend: suspendUser })
    adminMsg.value = suspendUser ? 'User suspended.' : 'User unsuspended.'
    suspendUserId.value = ''
  } catch (e:any) {
    adminErr.value = e?.response?.data?.message ?? 'Failed to update user.'
  }
}

async function hide(hideTrip:boolean) {
  adminMsg.value = ''
  adminErr.value = ''
  if (!isGuid(hideTripId.value)) {
    adminErr.value = 'Enter a valid trip ID (GUID).'
    return
  }
  try {
    await http.post(`/admin/trips/${hideTripId.value}/hide`, { hide: hideTrip })
    adminMsg.value = hideTrip ? 'Trip hidden.' : 'Trip unhidden.'
    hideTripId.value = ''
  } catch (e:any) {
    adminErr.value = e?.response?.data?.message ?? 'Failed to update trip.'
  }
}

async function verifyDriver(verified:boolean) {
  verifyMsg.value = ''
  verifyErr.value = ''
  if (!isGuid(verifyUserId.value)) {
    verifyErr.value = 'Enter a valid user ID (GUID).'
    return
  }
  try {
    await http.post(`/admin/users/${verifyUserId.value}/driver-verify`, { verified, note: verifyNote.value || null })
    verifyMsg.value = verified ? 'Driver verified.' : 'Driver marked unverified.'
    verifyUserId.value = ''
    verifyNote.value = ''
  } catch (e:any) {
    verifyErr.value = e?.response?.data?.message ?? 'Failed to update driver verification.'
  }
}

async function loadIncidents() {
  incidentsLoading.value = true
  incidentsErr.value = ''
  try {
    const resp = await http.get('/admin/safety-incidents', { params: { status: null, take: 50 } })
    incidents.value = resp.data ?? []
  } catch (e:any) {
    incidentsErr.value = e?.response?.data?.message ?? 'Failed to load incidents.'
  } finally {
    incidentsLoading.value = false
  }
}

async function resolveIncident(id:string, status:string) {
  incidentsErr.value = ''
  try {
    const note = prompt('Resolution note (optional):') ?? null
    await http.post(`/admin/safety-incidents/${id}/resolve`, { status, note })
    await loadIncidents()
  } catch (e:any) {
    incidentsErr.value = e?.response?.data?.message ?? 'Failed to resolve incident.'
  }
}

function isGuid(value: string) {
  return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/.test(value.trim())
}
</script>
