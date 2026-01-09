<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-6">
      <div class="card p-5">
        <div class="text-xl font-semibold">Safety</div>
        <p class="text-sm text-slate-600 mt-1">Report abuse, manage emergency contacts, and block users to avoid future matches.</p>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="font-semibold">Emergency contact</div>
            <p class="text-sm text-slate-600 mt-1">Share trip updates with someone you trust.</p>
          </div>
          <button class="btn-outline" @click="loadContact">Refresh</button>
        </div>

        <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="text-sm text-slate-600">Name</label>
            <input v-model="contactForm.name" class="input mt-1" placeholder="Trusted contact" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Phone</label>
            <input v-model="contactForm.phoneNumber" class="input mt-1" placeholder="+94..." />
            <p v-if="contactForm.phoneNumber && !isLkPhone(contactForm.phoneNumber)" class="text-xs text-red-600">Use Sri Lankan format (+94XXXXXXXXX or 07XXXXXXXX).</p>
          </div>
          <div>
            <label class="text-sm text-slate-600">Email (optional)</label>
            <input v-model="contactForm.email" class="input mt-1" placeholder="name@example.com" />
          </div>
          <label class="flex items-center gap-2 text-sm text-slate-700 mt-2">
            <input type="checkbox" v-model="contactForm.shareLiveTripsByDefault" />
            Share live trips by default
          </label>
        </div>

        <div class="mt-4 flex items-center gap-3">
          <button class="btn-primary" :disabled="contactSaving || !contactForm.name.trim()" @click="saveContact">
            <span v-if="contactSaving">Saving…</span>
            <span v-else>{{ contact?.id ? 'Update' : 'Save' }} contact</span>
          </button>
          <button class="btn-ghost" v-if="contact?.id" :disabled="contactSaving" @click="deleteContact">Remove</button>
          <p v-if="contactMsg" class="text-sm text-emerald-700">{{ contactMsg }}</p>
          <p v-if="contactErr" class="text-sm text-red-600">{{ contactErr }}</p>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between">
          <div>
            <div class="font-semibold">Panic / incident</div>
            <p class="text-sm text-slate-600 mt-1">Alerts admins with a high-priority incident.</p>
          </div>
          <button class="btn-outline" @click="submitPanic" :disabled="panicSending || !panicForm.summary.trim()">
            <span v-if="panicSending">Sending…</span>
            <span v-else>Panic</span>
          </button>
        </div>

        <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="text-sm text-slate-600">Type</label>
            <select v-model="panicForm.type" class="input mt-1">
              <option value="Panic">Panic</option>
              <option value="SafetyConcern">SafetyConcern</option>
              <option value="Medical">Medical</option>
              <option value="Harassment">Harassment</option>
            </select>
          </div>
          <div>
            <label class="text-sm text-slate-600">Trip ID (optional)</label>
            <input v-model="panicForm.tripId" class="input mt-1" placeholder="Trip ID" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Booking ID (optional)</label>
            <input v-model="panicForm.bookingId" class="input mt-1" placeholder="Booking ID" />
          </div>
          <div class="sm:col-span-2">
            <label class="text-sm text-slate-600">Summary</label>
            <textarea v-model="panicForm.summary" rows="2" class="textarea mt-1" placeholder="What happened?"></textarea>
          </div>
        </div>
        <p v-if="panicMsg" class="text-sm text-emerald-700 mt-2">{{ panicMsg }}</p>
        <p v-if="panicErr" class="text-sm text-red-600 mt-2">{{ panicErr }}</p>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between">
          <div>
            <div class="font-semibold">Share live trip</div>
            <p class="text-sm text-slate-600 mt-1">Create a share link for a trip.</p>
          </div>
          <button class="btn-outline" @click="createShareLink" :disabled="shareSending || !isGuid(shareForm.tripId)">
            <span v-if="shareSending">Creating…</span>
            <span v-else>Create link</span>
          </button>
        </div>

        <div class="mt-4 grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div>
            <label class="text-sm text-slate-600">Trip ID</label>
            <input v-model="shareForm.tripId" class="input mt-1" placeholder="Trip ID" />
            <p v-if="shareForm.tripId && !isGuid(shareForm.tripId)" class="text-xs text-red-600">Enter a valid ID.</p>
          </div>
          <div>
            <label class="text-sm text-slate-600">Expires (minutes)</label>
            <input v-model.number="shareForm.expiresMinutes" type="number" min="5" max="720" class="input mt-1" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Use emergency contact</label>
            <select v-model="shareForm.emergencyContactId" class="input mt-1">
              <option :value="null">No</option>
              <option v-if="contact?.id" :value="contact.id">Yes ({{ contact.name }})</option>
            </select>
          </div>
        </div>

        <div class="mt-3">
          <div v-if="shareLink" class="rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-800">
            <div class="font-semibold">Link created</div>
            <div class="mt-1 break-all">{{ shareLinkUrl }}</div>
            <div class="text-xs mt-1">Expires at {{ shareLink.expiresAt }}</div>
          </div>
          <p v-if="shareErr" class="text-sm text-red-600 mt-2">{{ shareErr }}</p>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Report an issue</div>
            <p class="text-sm text-slate-600 mt-1">Reports go to admins for review. Use it for harassment, scams, or unsafe behavior.</p>
          </div>
        </div>

        <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="text-sm text-slate-600">Target type</label>
            <select v-model="targetType" class="input mt-1">
              <option value="User">User</option>
              <option value="Trip">Trip</option>
              <option value="Booking">Booking</option>
            </select>
          </div>
          <div>
            <label class="text-sm text-slate-600">Target ID</label>
            <input v-model="targetId" class="input mt-1" placeholder="User ID" />
            <p class="text-xs text-slate-500 mt-1">From trip/booking URL or profile.</p>
          </div>
          <div class="sm:col-span-2">
            <label class="text-sm text-slate-600">Reason</label>
            <input v-model="reason" class="input mt-1" placeholder="e.g., harassment, scam, dangerous driving" />
          </div>
          <div class="sm:col-span-2">
            <label class="text-sm text-slate-600">Details (optional)</label>
            <textarea v-model="details" rows="3" class="textarea mt-1" placeholder="Add what happened. Do not include sensitive personal data."></textarea>
          </div>
        </div>

        <div class="mt-4 flex items-center gap-3">
          <button class="btn-primary" :disabled="reporting" @click="submitReport">Submit report</button>
          <p v-if="reportOk" class="text-sm text-emerald-700">Report submitted.</p>
          <p v-if="reportErr" class="text-sm text-red-600">{{ reportErr }}</p>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Blocked users</div>
            <p class="text-sm text-slate-600 mt-1">Blocked users will not appear in search results and cannot book your trips.</p>
          </div>
          <button class="btn-outline" @click="loadBlocks">Refresh</button>
        </div>

        <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div v-for="u in blocks" :key="u.id" class="rounded-2xl border border-slate-100 p-4 bg-white">
            <div class="flex items-center justify-between gap-3">
              <div class="min-w-0">
                <div class="font-semibold truncate">{{ u.displayName }}</div>
                <div class="text-xs text-slate-500 mt-1">ID: {{ u.id }}</div>
              </div>
              <button class="btn-ghost" @click="unblock(u.id)">Unblock</button>
            </div>
          </div>

          <div v-if="blocks.length===0" class="text-sm text-slate-600">No blocked users.</div>
        </div>

        <div class="mt-5 border-t border-slate-100 pt-4">
          <div class="font-semibold">Block a user</div>
          <div class="mt-2 flex flex-col sm:flex-row gap-3">
            <input v-model="blockId" class="input flex-1" placeholder="User ID" />
            <button class="btn-primary" @click="block">Block</button>
          </div>
          <p v-if="blockErr" class="text-sm text-red-600 mt-2">{{ blockErr }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot name="safety-sidebar" />
      <div class="card p-5">
        <div class="font-semibold">Emergency</div>
        <p class="text-sm text-slate-600 mt-2">If you're in danger, contact local emergency services immediately. Use HopTrip reports afterwards.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import AdSlot from '../components/AdSlot.vue'
import { EmergencyContact, ShareLinkResponse, http } from '../lib/api'

const targetType = ref<'User'|'Trip'|'Booking'>('User')
const targetId = ref('')
const reason = ref('')
const details = ref('')

const reporting = ref(false)
const reportOk = ref(false)
const reportErr = ref('')

async function submitReport() {
  reportOk.value = false
  reportErr.value = ''
  reporting.value = true
  try {
    const body:any = {
      targetType: targetType.value,
      reason: reason.value,
      details: details.value || null
    }
    if (targetType.value === 'User') body.targetUserId = targetId.value
    if (targetType.value === 'Trip') body.tripId = targetId.value
    if (targetType.value === 'Booking') body.bookingId = targetId.value

    await http.post('/reports', body)
    reportOk.value = true
    reason.value = ''
    details.value = ''
    targetId.value = ''
  } catch (e:any) {
    reportErr.value = e?.response?.data?.message ?? e?.message ?? 'Failed to submit report.'
  } finally {
    reporting.value = false
  }
}

// Emergency contact + panic + share link
const contact = ref<EmergencyContact|null>(null)
const contactForm = ref({ name: '', phoneNumber: '', email: '', shareLiveTripsByDefault: true })
const contactSaving = ref(false)
const contactMsg = ref('')
const contactErr = ref('')

async function loadContact() {
  contactErr.value = ''
  const resp = await http.get<EmergencyContact | null>('/safety/emergency-contact')
  contact.value = resp.data ?? null
  if (contact.value) {
    contactForm.value = {
      name: contact.value.name,
      phoneNumber: contact.value.phoneNumber ?? '',
      email: contact.value.email ?? '',
      shareLiveTripsByDefault: contact.value.shareLiveTripsByDefault
    }
  } else {
    contactForm.value = { name: '', phoneNumber: '', email: '', shareLiveTripsByDefault: true }
  }
}

async function saveContact() {
  contactSaving.value = true
  contactMsg.value = ''
  contactErr.value = ''
  if (contactForm.value.phoneNumber && !isLkPhone(contactForm.value.phoneNumber.trim())) {
    contactErr.value = 'Enter a valid Sri Lankan phone number.'
    contactSaving.value = false
    return
  }
  try {
    const resp = await http.put<EmergencyContact>('/safety/emergency-contact', {
      ...contactForm.value,
      name: contactForm.value.name.trim(),
      phoneNumber: contactForm.value.phoneNumber?.trim() || null,
      email: contactForm.value.email?.trim() || null
    })
    contact.value = resp.data
    contactMsg.value = 'Saved.'
  } catch (e:any) {
    contactErr.value = e?.response?.data?.message ?? 'Failed to save contact.'
  } finally {
    contactSaving.value = false
  }
}

async function deleteContact() {
  contactSaving.value = true
  contactMsg.value = ''
  contactErr.value = ''
  try {
    await http.delete('/safety/emergency-contact')
    contact.value = null
    contactForm.value = { name: '', phoneNumber: '', email: '', shareLiveTripsByDefault: true }
    contactMsg.value = 'Removed.'
  } catch (e:any) {
    contactErr.value = e?.response?.data?.message ?? 'Failed to remove contact.'
  } finally {
    contactSaving.value = false
  }
}

const panicForm = ref<{ tripId: string; bookingId: string; type: string; summary: string }>({
  tripId: '',
  bookingId: '',
  type: 'Panic',
  summary: ''
})
const panicSending = ref(false)
const panicMsg = ref('')
const panicErr = ref('')

async function submitPanic() {
  if (!panicForm.value.summary.trim()) {
    panicErr.value = 'Summary is required.'
    return
  }
  if (panicForm.value.tripId && !isGuid(panicForm.value.tripId)) {
    panicErr.value = 'Trip ID must be a valid ID.'
    return
  }
  if (panicForm.value.bookingId && !isGuid(panicForm.value.bookingId)) {
    panicErr.value = 'Booking ID must be a valid ID.'
    return
  }
  panicSending.value = true
  panicMsg.value = ''
  panicErr.value = ''
  try {
    await http.post('/safety/panic', {
      tripId: panicForm.value.tripId || null,
      bookingId: panicForm.value.bookingId || null,
      type: panicForm.value.type,
      summary: panicForm.value.summary.trim() || panicForm.value.type
    })
    panicMsg.value = 'Incident sent to admins.'
  } catch (e:any) {
    panicErr.value = e?.response?.data?.message ?? 'Failed to send incident.'
  } finally {
    panicSending.value = false
  }
}

const shareForm = ref<{ tripId: string; expiresMinutes: number; emergencyContactId: string | null }>({
  tripId: '',
  expiresMinutes: 60,
  emergencyContactId: null
})
const shareLink = ref<ShareLinkResponse|null>(null)
const shareSending = ref(false)
const shareErr = ref('')

const shareLinkUrl = computed(() => {
  if (!shareLink.value || !shareForm.value.tripId) return ''
  return `${window.location.origin}/api/safety/share-trip/${shareForm.value.tripId}/${shareLink.value.token}`
})

async function createShareLink() {
  if (!isGuid(shareForm.value.tripId)) {
    shareErr.value = 'Trip ID must be a valid ID.'
    return
  }
  if (!shareForm.value.expiresMinutes || shareForm.value.expiresMinutes < 5) {
    shareErr.value = 'Expiry must be at least 5 minutes.'
    return
  }
  shareSending.value = true
  shareErr.value = ''
  shareLink.value = null
  try {
    const resp = await http.post<ShareLinkResponse>('/safety/share-trip', {
      tripId: shareForm.value.tripId,
      expiresMinutes: shareForm.value.expiresMinutes,
      emergencyContactId: shareForm.value.emergencyContactId
    })
    shareLink.value = resp.data
  } catch (e:any) {
    shareErr.value = e?.response?.data?.message ?? 'Failed to create share link.'
  } finally {
    shareSending.value = false
  }
}

const blocks = ref<any[]>([])
const blockId = ref('')
const blockErr = ref('')

async function loadBlocks() {
  const resp = await http.get<any[]>('/blocks')
  blocks.value = resp.data ?? []
}
async function block() {
  blockErr.value = ''
  try {
    await http.post(`/blocks/${blockId.value}`)
    blockId.value = ''
    await loadBlocks()
  } catch (e:any) {
    blockErr.value = e?.response?.data?.message ?? e?.message ?? 'Failed to block.'
  }
}
async function unblock(id:string) {
  await http.delete(`/blocks/${id}`)
  await loadBlocks()
}

loadBlocks()
loadContact()

function isGuid(value: string) {
  return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/.test(value.trim())
}

function isLkPhone(value: string) {
  return /^(?:\+94|0)7\d{8}$/.test(value.trim())
}
</script>
