<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-6">
      <div class="card p-5">
        <div class="text-xl font-semibold">Safety</div>
        <p class="text-sm text-slate-600 mt-1">Report abuse and block users to avoid future matches.</p>
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
            <input v-model="targetId" class="input mt-1" placeholder="GUID" />
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
            <input v-model="blockId" class="input flex-1" placeholder="User GUID" />
            <button class="btn-primary" @click="block">Block</button>
          </div>
          <p v-if="blockErr" class="text-sm text-red-600 mt-2">{{ blockErr }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot />
      <div class="card p-5">
        <div class="font-semibold">Emergency</div>
        <p class="text-sm text-slate-600 mt-2">If you're in danger, contact local emergency services immediately. Use TripShare reports afterwards.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AdSlot from '../components/AdSlot.vue'
import { http } from '../lib/api'

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
</script>
