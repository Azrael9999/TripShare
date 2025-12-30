<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-4">
      <div class="card p-6">
        <div class="text-2xl font-semibold">My bookings</div>
        <p class="text-sm text-slate-600 mt-1">View your reservations and leave ratings after completion.</p>
      </div>

      <div class="card p-6">
        <div class="flex items-center gap-2">
          <button class="btn-ghost" :class="{ 'bg-slate-100': tab==='passenger' }" @click="tab='passenger'; load()">Passenger</button>
          <button class="btn-ghost" :class="{ 'bg-slate-100': tab==='driver' }" @click="tab='driver'; load()">Driver</button>
        </div>

        <div class="mt-4 space-y-3">
          <div v-if="loading" class="text-slate-600">Loading…</div>
          <div v-else-if="items.length===0" class="text-slate-600">No bookings yet.</div>

          <div v-for="b in items" :key="b.id" class="rounded-xl border border-slate-100 p-4">
            <div class="flex items-start justify-between">
              <div>
                <div class="font-semibold">Booking</div>
                <div class="text-sm text-slate-600 mt-1">Status: <span class="badge">{{ b.status }}</span></div>
                <div class="text-xs text-slate-500 mt-1">Progress: {{ b.progressStatus ?? '—' }}</div>
                <div class="text-sm text-slate-500 mt-1">{{ b.priceTotal }} {{ b.currency }} · Seats: {{ b.seats }}</div>
                <div v-if="etaForBooking(b.id)" class="text-xs text-emerald-700 mt-1">
                  ETA pickup {{ formatEta(etaForBooking(b.id).etaToPickupSeconds) }} · Drop {{ formatEta(etaForBooking(b.id).etaToDropoffSeconds) }}
                </div>
              </div>

              <div class="flex gap-2">
                <RouterLink :to="`/trips/${b.tripId}`" class="btn-ghost">View trip</RouterLink>
                <button v-if="tab==='passenger' && canCancel(b)" class="btn-ghost" @click="setStatus(b.id,'cancelled')">Cancel</button>
                <button v-if="tab==='driver' && b.status==='Pending'" class="btn-ghost" @click="setStatus(b.id,'accepted', true)">Accept</button>
                <button v-if="tab==='driver' && b.status==='Pending'" class="btn-ghost" @click="setStatus(b.id,'rejected', true)">Reject</button>
                <button v-if="tab==='driver' && b.status==='Accepted'" class="btn-primary" @click="setStatus(b.id,'completed', true)">Complete</button>
              </div>
            </div>

            <div v-if="b.status==='Completed'" class="mt-4 flex items-center justify-between">
              <div class="text-sm text-slate-600">Leave a rating</div>
              <button class="btn-primary" @click="openRating(b.id)">Rate</button>
            </div>
          </div>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-4">
      <AdSlot name="bookings-sidebar" />
      <div class="card p-6">
        <div class="text-lg font-semibold">Tip</div>
        <p class="text-sm text-slate-600 mt-1">Keep bookings fair: cancel early if your plans change.</p>
      </div>
    </aside>

    <div v-if="ratingOpen" class="fixed inset-0 bg-black/30 flex items-center justify-center p-4" @click.self="ratingOpen=false">
      <div class="card w-full max-w-md p-6">
        <div class="flex items-center justify-between">
          <div class="text-lg font-semibold">Rate</div>
          <button class="btn-ghost" @click="ratingOpen=false"><XMarkIcon class="h-5 w-5"/></button>
        </div>

        <div class="mt-4">
          <label class="text-sm text-slate-600">Stars (1-5)</label>
          <input v-model.number="stars" type="number" min="1" max="5" class="input mt-1" />
        </div>

        <div class="mt-3">
          <label class="text-sm text-slate-600">Comment (optional)</label>
          <textarea v-model="comment" rows="3" class="input mt-1"></textarea>
        </div>

        <button class="btn-primary mt-5 w-full justify-center" @click="submitRating">Submit</button>
        <p v-if="err" class="text-sm text-red-600 mt-3">{{ err }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { http } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'
import { XMarkIcon } from '@heroicons/vue/24/outline'

const tab = ref<'passenger'|'driver'>('passenger')
const loading = ref(false)
const items = ref<any[]>([])
const etaMap = ref<Record<string, any>>({})

async function load() {
  loading.value = true
  try {
    const url = tab.value === 'passenger' ? '/bookings/mine' : '/bookings/driver'
    const resp = await http.get(url)
    etaMap.value = {}
    items.value = resp.data
    await loadEtasForBookings()
  } finally {
    loading.value = false
  }
}
load()

function canCancel(b: any) {
  return b.status !== 'Rejected' && b.status !== 'Completed' && b.status !== 'Cancelled'
}

async function setStatus(id: string, status: string, driverAction = false) {
  const endpoint = driverAction ? `/bookings/${id}/status/driver` : `/bookings/${id}/status/passenger`
  await http.post(endpoint, { status })
  await load()
}

async function loadEtasForBookings() {
  const ids = Array.from(new Set(items.value.filter((b:any) => b.status === 'Accepted').map((b:any) => b.tripId)))
  for (const tripId of ids) {
    try {
      const resp = await http.get(`/trips/${tripId}/eta`)
      const etas = resp.data?.etas ?? []
      etas.forEach((e:any) => { etaMap.value[e.bookingId] = e })
    } catch {
      // ignore unauthorized trips
    }
  }
}

function etaForBooking(id:string) {
  return etaMap.value[id]
}

// Rating modal
const ratingOpen = ref(false)
const bookingId = ref('')
const stars = ref(5)
const comment = ref('')
const err = ref('')

function openRating(id: string) {
  bookingId.value = id
  stars.value = 5
  comment.value = ''
  err.value = ''
  ratingOpen.value = true
}

async function submitRating() {
  err.value = ''
  try {
    await http.post('/ratings', { bookingId: bookingId.value, stars: stars.value, comment: comment.value || null })
    ratingOpen.value = false
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Rating failed'
  }
}

function formatEta(sec?: number) {
  if (sec === undefined || sec === null) return 'N/A'
  const minutes = Math.max(0, Math.round(sec / 60))
  return minutes < 1 ? '<1 min' : `${minutes} min`
}
</script>
