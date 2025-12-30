<template>
  <div v-if="loading" class="text-slate-600">Loading…</div>
  <div v-else-if="!trip" class="card p-8 text-center">Trip not found</div>

  <div v-else class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-4">
      <div class="card p-6">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="badge">{{ dayjs(trip.departureTimeUtc).format('MMM D, YYYY HH:mm') }} UTC</div>
            <div class="mt-3 text-2xl font-semibold">
              {{ trip.routePoints[0]?.displayAddress }} → {{ trip.routePoints[trip.routePoints.length-1]?.displayAddress }}
            </div>
            <div class="mt-2 text-sm text-slate-600">
              Seats total: {{ trip.seatsTotal }} · Currency: {{ trip.currency }}
            </div>
          </div>
          <div class="flex items-center gap-3">
            <img v-if="trip.driverPhotoUrl" :src="trip.driverPhotoUrl" class="h-12 w-12 rounded-full" />
            <div>
              <div class="text-sm text-slate-500">Driver</div>
              <div class="font-semibold">{{ trip.driverName }}</div>
              <div class="flex flex-wrap gap-2 mt-1">
                <span v-if="trip.driverVerified" class="chip bg-emerald-50 text-emerald-700 border-emerald-100">Driver verified</span>
                <span v-else-if="trip.driverIdentityVerified" class="chip bg-emerald-50 text-emerald-700 border-emerald-100">ID verified</span>
                <span v-else-if="trip.driverEmailVerified" class="chip">Email verified</span>
              </div>
            </div>
          </div>
        </div>

        <div class="mt-3 flex flex-wrap items-center gap-2">
          <span class="chip">Status: {{ trip.status }}</span>
          <span v-if="trip.locationUpdatedAtUtc" class="chip">Location {{ dayjs(trip.locationUpdatedAtUtc).fromNow() }}</span>
        </div>
        <div
          v-if="trip.currentLat !== null && trip.currentLat !== undefined && trip.currentLng !== null && trip.currentLng !== undefined"
          class="mt-1 text-xs text-slate-600"
        >
          Driver: {{ Number(trip.currentLat).toFixed(4) }}, {{ Number(trip.currentLng).toFixed(4) }}
          <span v-if="trip.currentHeading !== null && trip.currentHeading !== undefined">· heading {{ trip.currentHeading }}°</span>
        </div>

        <div class="mt-5">
          <div class="text-sm font-semibold text-slate-700">Stops</div>
          <ol class="mt-3 space-y-2">
            <li v-for="rp in trip.routePoints" :key="rp.id" class="flex items-start gap-3">
              <div class="mt-1 h-2.5 w-2.5 rounded-full bg-brand-600"></div>
              <div>
                <div class="font-medium">{{ rp.displayAddress }}</div>
                <div class="text-xs text-slate-500">#{{ rp.orderIndex }} · {{ rp.type }}</div>
              </div>
            </li>
          </ol>
        </div>

        <div v-if="trip.notes" class="mt-5 text-sm text-slate-700 border-t border-slate-100 pt-4">
          {{ trip.notes }}
        </div>
      </div>

      <div class="card p-6">
        <div class="text-sm font-semibold text-slate-700">Segment prices</div>
        <div class="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div v-for="s in trip.segments" :key="s.id" class="rounded-xl border border-slate-100 p-4">
            <div class="text-sm text-slate-600">Segment #{{ s.orderIndex }}</div>
            <div class="font-medium mt-1">
              {{ nameOfPoint(s.fromRoutePointId) }} → {{ nameOfPoint(s.toRoutePointId) }}
            </div>
            <div class="text-lg font-semibold mt-2">{{ s.price }} {{ s.currency }}</div>
          </div>
        </div>
      </div>

      <div class="card p-6">
        <div class="flex items-center justify-between gap-3">
          <div class="text-sm font-semibold text-slate-700">Live ETA & status</div>
          <button class="btn-ghost" @click="loadEta" :disabled="etaLoading">Refresh</button>
        </div>
        <div class="mt-3 space-y-2 text-sm text-slate-700">
          <div>Status: <span class="badge">{{ trip.status }}</span></div>
          <div v-if="etaError" class="text-xs text-red-600">{{ etaError }}</div>
          <div v-else-if="etaResults.length===0" class="text-xs text-slate-500">No live ETA yet. Join the trip as a passenger or driver to see estimates.</div>
          <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div v-for="e in etaResults" :key="e.bookingId" class="rounded-xl border border-slate-100 p-3 bg-white">
              <div class="text-xs text-slate-500">Booking {{ e.bookingId }}</div>
              <div class="font-semibold mt-1">Pickup: {{ formatSeconds(e.etaToPickupSeconds) }}</div>
              <div class="text-sm text-slate-600">Drop-off: {{ formatSeconds(e.etaToDropoffSeconds) }}</div>
              <div class="text-xs text-slate-500 mt-1">Updated {{ dayjs(e.calculatedAtUtc).fromNow() }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Non-invasive ad slot: bottom of details -->
      <AdSlot name="trip-details-bottom" />
    </section>

    <aside class="lg:col-span-4 space-y-4">
      <div class="card p-6">
        <div class="text-lg font-semibold">Book seats</div>

        <div v-if="!auth.isAuthenticated" class="mt-2 text-sm text-slate-600">
          Please sign in to book.
        </div>

        <div v-else-if="!auth.me?.emailVerified" class="mt-2 text-sm text-slate-600">
          Your email is not verified yet. Check your inbox (dev emails are written to the API folder), or resend from your profile.
        </div>

        <div v-else class="mt-4 space-y-3">
          <div>
            <label class="text-sm text-slate-600">Pickup</label>
            <select v-model="pickupId" class="input mt-1">
              <option v-for="rp in trip.routePoints.slice(0, -1)" :key="rp.id" :value="rp.id">{{ rp.displayAddress }}</option>
            </select>
          </div>

          <div>
            <label class="text-sm text-slate-600">Dropoff</label>
            <select v-model="dropoffId" class="input mt-1">
              <option v-for="rp in trip.routePoints.slice(1)" :key="rp.id" :value="rp.id">{{ rp.displayAddress }}</option>
            </select>
          </div>

          <div class="rounded-2xl border border-slate-100 p-3 bg-slate-50 space-y-2">
            <div class="flex items-center justify-between">
              <div class="text-sm font-semibold text-slate-700">Pickup pin</div>
              <button class="btn-ghost text-xs" type="button" @click="snapPickupToStop">Snap to stop</button>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="text-xs text-slate-500">Lat</label>
                <input v-model.number="pickupPin.lat" type="number" step="0.0001" class="input mt-1" />
              </div>
              <div>
                <label class="text-xs text-slate-500">Lng</label>
                <input v-model.number="pickupPin.lng" type="number" step="0.0001" class="input mt-1" />
              </div>
            </div>
            <div>
              <label class="text-xs text-slate-500">Place label</label>
              <input v-model="pickupPin.placeName" type="text" class="input mt-1" placeholder="Apartment lobby, gate number, etc." />
            </div>
          </div>

          <div class="rounded-2xl border border-slate-100 p-3 bg-slate-50 space-y-2">
            <div class="flex items-center justify-between">
              <div class="text-sm font-semibold text-slate-700">Drop-off pin</div>
              <button class="btn-ghost text-xs" type="button" @click="snapDropoffToStop">Snap to stop</button>
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="text-xs text-slate-500">Lat</label>
                <input v-model.number="dropoffPin.lat" type="number" step="0.0001" class="input mt-1" />
              </div>
              <div>
                <label class="text-xs text-slate-500">Lng</label>
                <input v-model.number="dropoffPin.lng" type="number" step="0.0001" class="input mt-1" />
              </div>
            </div>
            <div>
              <label class="text-xs text-slate-500">Place label</label>
              <input v-model="dropoffPin.placeName" type="text" class="input mt-1" placeholder="Lobby, curbside, etc." />
            </div>
          </div>

          <div class="flex gap-3">
            <div class="flex-1">
              <label class="text-sm text-slate-600">Seats</label>
              <input v-model.number="seats" type="number" min="1" max="8" class="input mt-1" />
            </div>
            <div class="flex-1">
              <label class="text-sm text-slate-600">Estimated</label>
              <div class="mt-1 rounded-xl border border-slate-100 bg-slate-50 px-3 py-2 font-semibold">
                {{ estimateTotal() }} {{ trip.currency }}
              </div>
            </div>
          </div>

          <button class="btn-primary w-full justify-center" :disabled="booking" @click="book">
            <span v-if="booking">Booking…</span>
            <span v-else>Confirm booking</span>
          </button>

          <p class="text-xs text-slate-500">
            No online payment yet. You agree a price and pay the driver in person.
          </p>

          <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
        </div>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import { http } from '../lib/api'
import { useAuthStore } from '../stores/auth'
import AdSlot from '../components/AdSlot.vue'
dayjs.extend(relativeTime)

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const trip = ref<any | null>(null)
const loading = ref(true)

const pickupId = ref<string>('')
const dropoffId = ref<string>('')
const pickupPin = ref<{ lat: number; lng: number; placeName: string; placeId: string }>({ lat: 0, lng: 0, placeName: '', placeId: '' })
const dropoffPin = ref<{ lat: number; lng: number; placeName: string; placeId: string }>({ lat: 0, lng: 0, placeName: '', placeId: '' })
const seats = ref<number>(1)
const booking = ref(false)
const error = ref('')

const etaResults = ref<any[]>([])
const etaError = ref('')
const etaLoading = ref(false)
const refreshHandle = ref<number | undefined>()

async function load(initial = false) {
  if (initial) loading.value = true
  try {
    const resp = await http.get(`/trips/${route.params.id}`)
    trip.value = resp.data
    if (initial && trip.value?.routePoints?.length) {
      pickupId.value = trip.value.routePoints[0].id
      dropoffId.value = trip.value.routePoints[trip.value.routePoints.length - 1].id
      snapPickupToStop()
      snapDropoffToStop()
    }
    await loadEta()
    startLiveRefresh()
  } catch (e: any) {
    error.value = e?.response?.data?.message ?? 'Unable to load trip'
  } finally {
    if (initial) loading.value = false
  }
}
load(true)

function nameOfPoint(id: string) {
  return trip.value?.routePoints?.find((x: any) => x.id === id)?.displayAddress ?? 'Point'
}

function routePoint(id: string) {
  return trip.value?.routePoints?.find((x: any) => x.id === id)
}

function snapPickupToStop() {
  const rp = routePoint(pickupId.value)
  if (!rp) return
  pickupPin.value.lat = rp.lat
  pickupPin.value.lng = rp.lng
  pickupPin.value.placeName = rp.displayAddress
  pickupPin.value.placeId = rp.placeId ?? ''
}

function snapDropoffToStop() {
  const rp = routePoint(dropoffId.value)
  if (!rp) return
  dropoffPin.value.lat = rp.lat
  dropoffPin.value.lng = rp.lng
  dropoffPin.value.placeName = rp.displayAddress
  dropoffPin.value.placeId = rp.placeId ?? ''
}

watch(pickupId, snapPickupToStop)
watch(dropoffId, snapDropoffToStop)

function estimateTotal() {
  if (!trip.value) return 0
  const rps = trip.value.routePoints
  const pi = rps.findIndex((x: any) => x.id === pickupId.value)
  const di = rps.findIndex((x: any) => x.id === dropoffId.value)
  if (pi < 0 || di < 0 || di <= pi) return 0
  const segs = trip.value.segments.filter((s: any) => s.orderIndex >= pi && s.orderIndex <= di - 1)
  const perSeat = segs.reduce((sum: number, s: any) => sum + s.price, 0)
  return perSeat * (seats.value || 1)
}

async function book() {
  error.value = ''
  booking.value = true
  try {
    await http.post('/bookings', {
      tripId: trip.value.id,
      pickupRoutePointId: pickupId.value,
      dropoffRoutePointId: dropoffId.value,
      pickupLat: pickupPin.value.lat,
      pickupLng: pickupPin.value.lng,
      dropoffLat: dropoffPin.value.lat,
      dropoffLng: dropoffPin.value.lng,
      pickupPlaceName: pickupPin.value.placeName || nameOfPoint(pickupId.value),
      pickupPlaceId: pickupPin.value.placeId || routePoint(pickupId.value)?.placeId || null,
      dropoffPlaceName: dropoffPin.value.placeName || nameOfPoint(dropoffId.value),
      dropoffPlaceId: dropoffPin.value.placeId || routePoint(dropoffId.value)?.placeId || null,
      seats: seats.value
    })
    router.push('/bookings')
  } catch (e: any) {
    error.value = e?.response?.data?.message ?? 'Booking failed'
  } finally {
    booking.value = false
  }
}

async function refreshTrip() {
  try {
    const resp = await http.get(`/trips/${route.params.id}`)
    trip.value = trip.value ? { ...trip.value, ...resp.data } : resp.data
  } catch {
    // ignore transient errors during polling
  }
}

async function loadEta() {
  etaError.value = ''
  etaLoading.value = true
  try {
    if (!auth.isAuthenticated) {
      etaResults.value = []
      return
    }
    const resp = await http.get(`/trips/${route.params.id}/eta`)
    etaResults.value = resp.data?.etas ?? []
  } catch (e: any) {
    etaResults.value = []
    etaError.value = e?.response?.data?.message ?? 'Live ETA unavailable for this trip.'
  } finally {
    etaLoading.value = false
  }
}

function startLiveRefresh() {
  if (refreshHandle.value) return
  refreshHandle.value = window.setInterval(async () => {
    await refreshTrip()
    await loadEta()
  }, 15000)
}

function formatSeconds(sec: number) {
  if (!sec && sec !== 0) return 'N/A'
  const minutes = Math.max(0, Math.round(sec / 60))
  return minutes < 1 ? '<1 min' : `${minutes} min`
}

onBeforeUnmount(() => {
  if (refreshHandle.value) window.clearInterval(refreshHandle.value)
})
</script>
