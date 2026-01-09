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

      <div class="card p-5">
        <div>
          <div class="font-semibold">Identity verification</div>
          <p class="text-sm text-slate-600 mt-1">Review driver documents to unlock verified badges.</p>
          <RouterLink to="/admin/identity" class="btn-outline mt-3 inline-flex items-center">Review submissions</RouterLink>
        </div>
      </div>

      <div v-if="isSuperAdmin" class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Admin access</div>
            <p class="text-sm text-slate-600 mt-1">Create and approve admin accounts.</p>
          </div>
          <RouterLink to="/admin/admins" class="btn-outline">Manage admins</RouterLink>
        </div>
      </div>

      <div v-if="isSuperAdmin" class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Site settings</div>
            <p class="text-sm text-slate-600 mt-1">Manage ads, branding, maps, and driver verification gate.</p>
          </div>
          <RouterLink to="/admin/site-settings" class="btn-outline">Open settings</RouterLink>
        </div>
      </div>

      <div class="card p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        <div class="space-y-2">
          <div class="font-semibold">Suspend / unsuspend user</div>
          <input
            v-model="suspendUserQuery"
            class="input"
            placeholder="Search drivers by name, email, phone, vehicle, or user ID"
            title="Search drivers by name, email, phone number, vehicle details (make/model/color/plate), or user ID."
          />
          <div v-if="suspendSearchLoading" class="text-xs text-slate-500 mt-2">Searching drivers…</div>
          <div v-if="suspendSuggestions.length" class="mt-2 space-y-2">
            <button
              v-for="driver in suspendSuggestions"
              :key="driver.id"
              class="w-full rounded-xl border border-slate-100 bg-white p-3 text-left text-sm hover:border-slate-200"
              @click="selectSuspendDriver(driver)"
            >
              <div class="font-semibold">{{ driver.displayName || driver.email }}</div>
              <div class="text-xs text-slate-500">
                {{ driver.email }} · {{ driver.phoneNumber || 'No phone' }} · {{ driver.vehicleSummary || 'No vehicle' }}
              </div>
              <div class="text-xs text-slate-400">ID: {{ driver.id }}</div>
            </button>
          </div>
          <textarea v-model="suspendNote" class="textarea" rows="2" placeholder="Reason for suspension change"></textarea>
          <div class="flex gap-2">
            <button class="btn-outline" :disabled="!suspendUserId || !hasNote(suspendNote)" @click="suspend(true)">Suspend</button>
            <button class="btn-ghost" :disabled="!suspendUserId || !hasNote(suspendNote)" @click="suspend(false)">Unsuspend</button>
          </div>
        </div>
        <div class="space-y-2">
          <div class="font-semibold">Hide / unhide trip</div>
          <input v-model="hideTripId" class="input" placeholder="Trip ID" title="Trip ID (UUID format)" />
          <textarea v-model="hideNote" class="textarea" rows="2" placeholder="Reason for visibility change"></textarea>
          <div class="flex gap-2">
            <button class="btn-outline" :disabled="!isGuid(hideTripId) || !hasNote(hideNote)" @click="hide(true)">Hide</button>
            <button class="btn-ghost" :disabled="!isGuid(hideTripId) || !hasNote(hideNote)" @click="hide(false)">Unhide</button>
          </div>
        </div>
      </div>

      <div class="card p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        <div class="space-y-2">
          <div class="font-semibold">Driver verification</div>
          <input
            v-model="verifyUserQuery"
            class="input"
            placeholder="Search drivers by name, email, phone, vehicle, or user ID"
            title="Search drivers by name, email, phone number, vehicle details (make/model/color/plate), or user ID."
          />
          <div v-if="verifySearchLoading" class="text-xs text-slate-500 mt-2">Searching drivers…</div>
          <div v-if="verifySuggestions.length" class="mt-2 space-y-2">
            <button
              v-for="driver in verifySuggestions"
              :key="driver.id"
              class="w-full rounded-xl border border-slate-100 bg-white p-3 text-left text-sm hover:border-slate-200"
              @click="selectVerifyDriver(driver)"
            >
              <div class="font-semibold">{{ driver.displayName || driver.email }}</div>
              <div class="text-xs text-slate-500">
                {{ driver.email }} · {{ driver.phoneNumber || 'No phone' }} · {{ driver.vehicleSummary || 'No vehicle' }}
              </div>
              <div class="text-xs text-slate-400">ID: {{ driver.id }}</div>
            </button>
          </div>
          <textarea v-model="verifyNote" class="textarea" rows="2" placeholder="Optional note"></textarea>
          <div class="flex gap-2">
            <button class="btn-outline" :disabled="!verifyUserId" @click="verifyDriver(true)">Mark verified</button>
            <button class="btn-ghost" :disabled="!verifyUserId" @click="verifyDriver(false)">Unverify</button>
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
import { computed, ref, watch } from 'vue'
import { http } from '../../lib/api'
import AdSlot from '../../components/AdSlot.vue'
import { useAuthStore } from '../../stores/auth'

const metrics = ref<any|null>(null)
const adminMsg = ref('')
const adminErr = ref('')
type DriverSearchResult = {
  id: string
  displayName?: string | null
  email?: string | null
  phoneNumber?: string | null
  vehicleSummary?: string | null
}

const suspendUserQuery = ref('')
const suspendUserId = ref('')
const suspendNote = ref('')
const hideTripId = ref('')
const hideNote = ref('')
const verifyUserQuery = ref('')
const verifyUserId = ref('')
const verifyNote = ref('')
const verifyMsg = ref('')
const verifyErr = ref('')
const incidents = ref<any[]>([])
const incidentsLoading = ref(false)
const incidentsErr = ref('')
const auth = useAuthStore()
const isSuperAdmin = computed(() => auth.me?.role === 'superadmin')
const suspendSuggestions = ref<DriverSearchResult[]>([])
const verifySuggestions = ref<DriverSearchResult[]>([])
const suspendSearchLoading = ref(false)
const verifySearchLoading = ref(false)
let suspendSearchTimer: ReturnType<typeof setTimeout> | null = null
let verifySearchTimer: ReturnType<typeof setTimeout> | null = null

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
  const metricsResp = await http.get('/admin/metrics')
  metrics.value = metricsResp.data
  await loadIncidents()
}
load()

async function suspend(suspendUser:boolean) {
  adminMsg.value = ''
  adminErr.value = ''
  if (!suspendUserId.value) {
    adminErr.value = 'Select a driver to update.'
    return
  }
  if (!hasNote(suspendNote.value)) {
    adminErr.value = 'Provide a suspension note before updating the user.'
    return
  }
  try {
    await http.post(`/admin/users/${suspendUserId.value}/suspend`, { suspend: suspendUser, note: suspendNote.value.trim() })
    adminMsg.value = suspendUser ? 'User suspended.' : 'User unsuspended.'
    suspendUserQuery.value = ''
    suspendUserId.value = ''
    suspendNote.value = ''
    suspendSuggestions.value = []
  } catch (e:any) {
    adminErr.value = e?.response?.data?.message ?? 'Failed to update user.'
  }
}

async function hide(hideTrip:boolean) {
  adminMsg.value = ''
  adminErr.value = ''
  if (!isGuid(hideTripId.value)) {
    adminErr.value = 'Enter a valid trip ID.'
    return
  }
  if (!hasNote(hideNote.value)) {
    adminErr.value = 'Provide a visibility note before updating the trip.'
    return
  }
  try {
    await http.post(`/admin/trips/${hideTripId.value}/hide`, { hide: hideTrip, note: hideNote.value.trim() })
    adminMsg.value = hideTrip ? 'Trip hidden.' : 'Trip unhidden.'
    hideTripId.value = ''
    hideNote.value = ''
  } catch (e:any) {
    adminErr.value = e?.response?.data?.message ?? 'Failed to update trip.'
  }
}

async function verifyDriver(verified:boolean) {
  verifyMsg.value = ''
  verifyErr.value = ''
  if (!verifyUserId.value) {
    verifyErr.value = 'Select a driver to update.'
    return
  }
  try {
    await http.post(`/admin/users/${verifyUserId.value}/driver-verify`, { verified, note: verifyNote.value || null })
    verifyMsg.value = verified ? 'Driver verified.' : 'Driver marked unverified.'
    verifyUserQuery.value = ''
    verifyUserId.value = ''
    verifyNote.value = ''
    verifySuggestions.value = []
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

function hasNote(value: string) {
  return value.trim().length > 0
}

async function fetchDriverSuggestions(query: string) {
  const resp = await http.get<DriverSearchResult[]>('/admin/drivers/search', { params: { query } })
  return resp.data ?? []
}

function selectSuspendDriver(driver: DriverSearchResult) {
  suspendUserId.value = driver.id
  suspendUserQuery.value = driver.displayName || driver.email || driver.id
  suspendSuggestions.value = []
}

function selectVerifyDriver(driver: DriverSearchResult) {
  verifyUserId.value = driver.id
  verifyUserQuery.value = driver.displayName || driver.email || driver.id
  verifySuggestions.value = []
}

watch(suspendUserQuery, (value) => {
  const trimmed = value.trim()
  if (isGuid(trimmed)) {
    suspendUserId.value = trimmed
    suspendSuggestions.value = []
    return
  }
  suspendUserId.value = ''
  if (trimmed.length < 3) {
    suspendSuggestions.value = []
    if (suspendSearchTimer) clearTimeout(suspendSearchTimer)
    return
  }
  if (suspendSearchTimer) clearTimeout(suspendSearchTimer)
  suspendSearchTimer = setTimeout(async () => {
    suspendSearchLoading.value = true
    try {
      suspendSuggestions.value = await fetchDriverSuggestions(trimmed)
    } catch {
      suspendSuggestions.value = []
    } finally {
      suspendSearchLoading.value = false
    }
  }, 250)
})

watch(verifyUserQuery, (value) => {
  const trimmed = value.trim()
  if (isGuid(trimmed)) {
    verifyUserId.value = trimmed
    verifySuggestions.value = []
    return
  }
  verifyUserId.value = ''
  if (trimmed.length < 3) {
    verifySuggestions.value = []
    if (verifySearchTimer) clearTimeout(verifySearchTimer)
    return
  }
  if (verifySearchTimer) clearTimeout(verifySearchTimer)
  verifySearchTimer = setTimeout(async () => {
    verifySearchLoading.value = true
    try {
      verifySuggestions.value = await fetchDriverSuggestions(trimmed)
    } catch {
      verifySuggestions.value = []
    } finally {
      verifySearchLoading.value = false
    }
  }, 250)
})
</script>
