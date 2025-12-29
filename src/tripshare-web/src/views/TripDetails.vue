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
            </div>
          </div>
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

      <!-- Non-invasive ad slot: bottom of details -->
      <AdSlot />
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
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import { http } from '../lib/api'
import { useAuthStore } from '../stores/auth'
import AdSlot from '../components/AdSlot.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const trip = ref<any | null>(null)
const loading = ref(true)

const pickupId = ref<string>('')
const dropoffId = ref<string>('')
const seats = ref<number>(1)
const booking = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  try {
    const resp = await http.get(`/trips/${route.params.id}`)
    trip.value = resp.data
    pickupId.value = trip.value.routePoints[0].id
    dropoffId.value = trip.value.routePoints[trip.value.routePoints.length - 1].id
  } finally {
    loading.value = false
  }
}
load()

function nameOfPoint(id: string) {
  return trip.value?.routePoints?.find((x: any) => x.id === id)?.displayAddress ?? 'Point'
}

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
      seats: seats.value
    })
    router.push('/bookings')
  } catch (e: any) {
    error.value = e?.response?.data?.message ?? 'Booking failed'
  } finally {
    booking.value = false
  }
}
</script>
