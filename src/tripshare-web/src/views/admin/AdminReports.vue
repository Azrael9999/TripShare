<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8">
      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">Reports</div>
            <p class="text-sm text-slate-600 mt-1">Review user/trip/booking reports and take action.</p>
          </div>
          <div class="flex gap-2">
            <select v-model="status" class="input">
              <option value="">All</option>
              <option value="Open">Open</option>
              <option value="Investigating">Investigating</option>
              <option value="Resolved">Resolved</option>
              <option value="Rejected">Rejected</option>
            </select>
            <button class="btn-outline" @click="load">Refresh</button>
          </div>
        </div>

        <div class="mt-4 space-y-3">
          <div v-if="loading" class="text-slate-600">Loading...</div>
          <div v-else-if="items.length===0" class="text-slate-600">No reports.</div>

          <div v-for="r in items" :key="r.id" class="rounded-2xl border border-slate-100 p-4 bg-white">
            <div class="flex items-start justify-between gap-4">
              <div class="min-w-0">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="badge">{{ r.status }}</span>
                  <span class="chip">{{ r.targetType }}</span>
                  <span class="text-xs text-slate-500">{{ r.createdAtUtc }}</span>
                </div>
                <div class="mt-2 font-semibold">{{ r.reason }}</div>
                <div class="mt-1 text-sm text-slate-700 whitespace-pre-wrap">{{ r.details }}</div>

                <div class="mt-2 text-xs text-slate-500">
                  TargetUser: {{ r.targetUserId || '-' }} • Trip: {{ r.tripId || '-' }} • Booking: {{ r.bookingId || '-' }}
                </div>
              </div>

              <div class="flex flex-col gap-2 items-end">
                <button class="btn-outline" @click="update(r.id, 'Investigating')">Investigating</button>
                <button class="btn-outline" @click="update(r.id, 'Resolved')">Resolved</button>
                <button class="btn-outline" @click="update(r.id, 'Rejected')">Rejected</button>
              </div>
            </div>
          </div>
        </div>

        <p v-if="msg" class="text-sm text-emerald-700 mt-4">{{ msg }}</p>
        <p v-if="err" class="text-sm text-red-600 mt-4">{{ err }}</p>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot />
      <div class="card p-5">
        <div class="font-semibold">Moderation</div>
        <p class="text-sm text-slate-600 mt-2">
          Use Admin tools to suspend users or hide trips when necessary. Keep clear notes for audit trails.
        </p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { http } from '../../lib/api'
import AdSlot from '../../components/AdSlot.vue'

const status = ref<string>('')
const loading = ref(false)
const items = ref<any[]>([])
const msg = ref('')
const err = ref('')

async function load() {
  msg.value = ''
  err.value = ''
  loading.value = true
  try {
    const resp = await http.get<any[]>('/reports', { params: { status: status.value || null, take: 100 } })
    items.value = resp.data ?? []
  } finally {
    loading.value = false
  }
}

async function update(id:string, newStatus:string) {
  msg.value = ''
  err.value = ''
  try {
    const note = prompt('Admin note (optional):') ?? null
    await http.post(`/reports/${id}`, { status: newStatus, adminNote: note })
    msg.value = 'Updated.'
    await load()
  } catch (e:any) {
    err.value = e?.response?.data?.message ?? e?.message ?? 'Failed.'
  }
}

watch(status, () => load())
load()
</script>
