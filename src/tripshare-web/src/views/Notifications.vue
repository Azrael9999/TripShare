<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8">
      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">Notifications</div>
            <p class="text-sm text-slate-600 mt-1">Booking updates, trip changes, and safety notices.</p>
          </div>
          <label class="chip">
            <input type="checkbox" v-model="unreadOnly" class="mr-2" />
            Unread only
          </label>
        </div>

        <div class="mt-4 space-y-3">
          <div v-if="loading" class="text-slate-600">Loading...</div>
          <div v-else-if="items.length===0" class="text-slate-600">No notifications.</div>

          <div v-for="n in items" :key="n.id" class="rounded-2xl border border-slate-100 p-4 hover:border-brand-200 transition bg-white">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <span class="badge">{{ n.type }}</span>
                  <span class="text-xs text-slate-500">{{ dayjs(n.createdAtUtc).fromNow() }}</span>
                  <span v-if="!n.isRead" class="chip">New</span>
                </div>
                <div class="mt-2 font-semibold truncate">{{ n.title }}</div>
                <div class="mt-1 text-sm text-slate-700 whitespace-pre-wrap">{{ n.message }}</div>

                <div class="mt-3 flex flex-wrap gap-2">
                  <RouterLink v-if="n.relatedTripId" :to="`/trips/${n.relatedTripId}`" class="btn-outline">Open trip</RouterLink>
                  <RouterLink v-if="n.relatedBookingId" to="/bookings" class="btn-outline">Open bookings</RouterLink>
                </div>
              </div>

              <button v-if="!n.isRead" class="btn-ghost" @click="markRead(n.id)">Mark read</button>
            </div>
          </div>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot />
      <div class="card p-5">
        <div class="font-semibold">Tip</div>
        <p class="text-sm text-slate-600 mt-2">Enable browser notifications after deployment to increase booking conversions (optional).</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import { http, NotificationItem } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'
dayjs.extend(relativeTime)

const unreadOnly = ref(false)
const loading = ref(false)
const items = ref<NotificationItem[]>([])

async function load() {
  loading.value = true
  try {
    const resp = await http.get<NotificationItem[]>('/notifications', { params: { unreadOnly: unreadOnly.value, take: 50 } })
    items.value = resp.data ?? []
  } finally {
    loading.value = false
  }
}

async function markRead(id: string) {
  await http.post(`/notifications/${id}/read`)
  await load()
}

watch(unreadOnly, () => load())
load()
</script>
