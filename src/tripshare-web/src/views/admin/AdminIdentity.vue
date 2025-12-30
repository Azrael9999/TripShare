<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8">
      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">Identity verifications</div>
            <p class="text-sm text-slate-600 mt-1">Approve or reject submitted documents.</p>
          </div>
          <div class="flex gap-2">
            <select v-model="status" class="input">
              <option value="">All</option>
              <option value="Pending">Pending</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
            </select>
            <button class="btn-outline" @click="load">Refresh</button>
          </div>
        </div>

        <div class="mt-4 space-y-3">
          <div v-if="loading" class="text-slate-600">Loading…</div>
          <div v-else-if="items.length === 0" class="text-slate-600">No records.</div>

          <div v-for="item in items" :key="item.id" class="rounded-2xl border border-slate-100 p-4 bg-white">
            <div class="flex items-start justify-between gap-4">
              <div class="min-w-0">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="badge">{{ item.status }}</span>
                  <span class="chip">{{ item.documentType }}</span>
                  <span class="text-xs text-slate-500">Submitted {{ item.submittedAtUtc }}</span>
                  <span v-if="item.reviewedAtUtc" class="text-xs text-slate-500">Reviewed {{ item.reviewedAtUtc }}</span>
                </div>
                <div class="mt-2 font-semibold">Ref: {{ item.documentReference }}</div>
                <div class="text-sm text-slate-600">KYC: {{ item.kycProvider || '-' }} / {{ item.kycReference || '-' }}</div>
                <div class="text-xs text-slate-500 mt-1">ID: {{ item.id }}</div>
              </div>
              <div class="flex flex-col gap-2">
                <button class="btn-outline" @click="review(item.id, true)">Approve</button>
                <button class="btn-outline" @click="review(item.id, false)">Reject</button>
              </div>
            </div>
          </div>
        </div>

        <p v-if="msg" class="text-sm text-emerald-700 mt-3">{{ msg }}</p>
        <p v-if="err" class="text-sm text-red-600 mt-3">{{ err }}</p>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot name="admin-sidebar" />
      <div class="card p-5">
        <div class="font-semibold">Guidance</div>
        <p class="text-sm text-slate-600 mt-2">Approved drivers unlock badges and trip creation when the gate is enabled.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { http } from '../../lib/api'
import AdSlot from '../../components/AdSlot.vue'

const status = ref<string>('Pending')
const loading = ref(false)
const items = ref<any[]>([])
const msg = ref('')
const err = ref('')

async function load() {
  loading.value = true
  err.value = ''
  try {
    const resp = await http.get('/admin/identity-verifications', { params: { status: status.value || null, take: 100 } })
    items.value = resp.data ?? []
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Failed to load.'
  } finally {
    loading.value = false
  }
}

async function review(id: string, approve: boolean) {
  msg.value = ''
  err.value = ''
  const note = prompt('Note (optional):') ?? undefined
  try {
    await http.post(`/admin/identity-verifications/${id}/review`, { approve, note })
    msg.value = 'Saved.'
    await load()
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Failed to save review.'
  }
}

watch(status, () => load())
load()
</script>
