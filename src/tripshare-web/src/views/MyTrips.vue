<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-5 space-y-4">
      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">My trips</div>
            <p class="text-sm text-slate-600 mt-1">Manage your trips and booking requests.</p>
          </div>
          <RouterLink to="/create" class="btn-primary">Create</RouterLink>
        </div>
      </div>

      <div class="space-y-3">
        <div v-if="loading" class="card p-5 text-slate-600">Loading...</div>

        <button
          v-for="t in trips"
          :key="t.id"
          class="w-full text-left card p-4 hover:border-brand-200 transition"
          :class="selectedTripId===t.id ? 'border-brand-300 ring-4 ring-brand-50' : ''"
          @click="selectTrip(t.id)"
        >
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <div class="font-semibold truncate">{{ startAddr(t) }} → {{ endAddr(t) }}</div>
              <div class="mt-1 text-xs text-slate-500">{{ dayjs(t.departureTimeUtc).format('MMM D, YYYY HH:mm') }} UTC</div>
              <div class="mt-2 flex flex-wrap gap-2">
                <span class="chip">Seats {{ t.seatsTotal }}</span>
                <span class="chip" v-if="t.instantBook">Instant</span>
                <span class="chip">{{ t.isPublic ? 'Public' : 'Hidden' }}</span>
                <span class="chip">Status: {{ t.status }}</span>
              </div>
            </div>
          </div>
        </button>

        <div v-if="!loading && trips.length===0" class="card p-5 text-slate-600">
          No trips yet. Create one.
        </div>
      </div>
    </section>

    <section class="lg:col-span-7 space-y-6">
      <div v-if="selectedTrip" class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-lg font-semibold">Trip controls</div>
            <p class="text-sm text-slate-600 mt-1">{{ startAddr(selectedTrip) }} → {{ endAddr(selectedTrip) }}</p>
          </div>
          <RouterLink :to="`/trips/${selectedTrip.id}`" class="btn-outline">Open trip</RouterLink>
        </div>

        <div class="mt-4 flex flex-wrap gap-2">
          <button class="btn-outline" @click="setVisibility(true)" :disabled="busy">Make public</button>
          <button class="btn-outline" @click="setVisibility(false)" :disabled="busy">Hide</button>
          <button class="btn-outline" @click="startTrip" :disabled="busy">Start</button>
          <button class="btn-outline" @click="completeTrip" :disabled="busy">Complete</button>
          <button class="btn-outline" @click="cancelTrip" :disabled="busy">Cancel</button>
        </div>

        <p v-if="msg" class="text-sm text-emerald-700 mt-3">{{ msg }}</p>
        <p v-if="err" class="text-sm text-red-600 mt-3">{{ err }}</p>
      </div>

      <div v-if="selectedTrip" class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-lg font-semibold">Booking requests</div>
            <p class="text-sm text-slate-600 mt-1">Accept/reject bookings. Accepted bookings can view contact details.</p>
          </div>
          <button class="btn-outline" @click="loadBookings" :disabled="busy">Refresh</button>
        </div>

        <div class="mt-4 space-y-3">
          <div v-if="bookingsLoading" class="text-slate-600">Loading...</div>
          <div v-else-if="tripBookings.length===0" class="text-slate-600">No booking requests.</div>

          <div v-for="b in tripBookings" :key="b.id" class="rounded-2xl border border-slate-100 p-4 bg-white">
            <div class="flex items-start justify-between gap-4">
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <span class="badge">{{ b.status }}</span>
                  <span class="text-xs text-slate-500">{{ dayjs(b.createdAtUtc).fromNow() }}</span>
                </div>
                <div class="mt-2 text-sm text-slate-700">
                  Seats: <span class="font-semibold">{{ b.seats }}</span>
                  • Price: <span class="font-semibold">{{ b.totalPrice }} {{ b.currency }}</span>
                </div>
                <div class="mt-2 text-xs text-slate-500">
                  Booking ID: {{ b.id }}
                </div>
              </div>

              <div class="flex flex-col gap-2 items-end">
                <div class="flex gap-2">
                  <button class="btn-outline" @click="setBookingStatus(b.id, 'Accepted')" :disabled="busy || b.status!=='Pending'">Accept</button>
                  <button class="btn-outline" @click="setBookingStatus(b.id, 'Rejected')" :disabled="busy || b.status!=='Pending'">Reject</button>
                </div>
                <button class="btn-ghost" @click="openContact(b.id)" :disabled="busy || b.status!=='Accepted'">View contact</button>
              </div>
            </div>

            <div v-if="contact[b.id]" class="mt-3 text-sm text-slate-700 rounded-xl bg-slate-50 border border-slate-100 p-3">
              <div class="font-semibold">Contact</div>
              <pre class="whitespace-pre-wrap text-xs mt-2">{{ contact[b.id] }}</pre>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="card p-5 text-slate-600">
        Select a trip to manage.
      </div>

      <AdSlot />
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import { http } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'
dayjs.extend(relativeTime)

type TripRow = any

const loading = ref(false)
const trips = ref<TripRow[]>([])
const selectedTripId = ref<string>('')

const busy = ref(false)
const msg = ref('')
const err = ref('')

const bookingsLoading = ref(false)
const bookings = ref<any[]>([])
const contact = ref<Record<string, string>>({})

function startAddr(t:any){ return t.routePoints?.[0]?.displayAddress ?? 'Start' }
function endAddr(t:any){ const rp=t.routePoints??[]; return rp.length? rp[rp.length-1].displayAddress:'End' }

const selectedTrip = computed(() => trips.value.find(t => t.id===selectedTripId.value) ?? null)
const tripBookings = computed(() => bookings.value.filter(b => b.tripId === selectedTripId.value).map(mapBooking))

function mapBooking(b:any){
  return {
    id: b.id ?? b.Id,
    tripId: b.tripId ?? b.TripId,
    status: b.status ?? b.Status,
    seats: b.seats ?? b.Seats,
    totalPrice: b.priceTotal ?? b.PriceTotal,
    currency: b.currency ?? b.Currency,
    createdAtUtc: (b.createdAt ?? b.CreatedAt) as string
  }
}

async function loadTrips() {
  loading.value = true
  try {
    const resp = await http.get<any>('/trips/mine', { params: { page: 1, pageSize: 50 } })
    trips.value = resp.data.items ?? resp.data.Items ?? []
    if (!selectedTripId.value && trips.value.length) selectedTripId.value = trips.value[0].id ?? trips.value[0].Id
  } finally {
    loading.value = false
  }
}

async function selectTrip(id:string){
  selectedTripId.value = id
  await loadBookings()
}

async function loadBookings(){
  if (!selectedTripId.value) return
  bookingsLoading.value = true
  try {
    const resp = await http.get<any[]>('/bookings/driver')
    bookings.value = resp.data ?? []
    contact.value = {}
  } finally {
    bookingsLoading.value = false
  }
}

async function setVisibility(isPublic:boolean){
  if (!selectedTripId.value) return
  msg.value=''; err.value=''
  busy.value=true
  try{
    await http.post(`/trips/${selectedTripId.value}/visibility`, { isPublic })
    msg.value = isPublic ? 'Trip is now public.' : 'Trip is now hidden.'
    await loadTrips()
  }catch(e:any){
    err.value = e?.response?.data?.message ?? e?.message ?? 'Failed.'
  }finally{ busy.value=false }
}
async function startTrip(){
  if (!selectedTripId.value) return
  msg.value=''; err.value=''
  busy.value=true
  try{
    await http.post(`/trips/${selectedTripId.value}/start`)
    msg.value = 'Trip started.'
    await loadTrips()
  }catch(e:any){ err.value = e?.response?.data?.message ?? e?.message ?? 'Failed.' }
  finally{ busy.value=false }
}
async function completeTrip(){
  if (!selectedTripId.value) return
  msg.value=''; err.value=''
  busy.value=true
  try{
    await http.post(`/trips/${selectedTripId.value}/complete`)
    msg.value = 'Trip completed.'
    await loadTrips()
  }catch(e:any){ err.value = e?.response?.data?.message ?? e?.message ?? 'Failed.' }
  finally{ busy.value=false }
}
async function cancelTrip(){
  if (!selectedTripId.value) return
  const reason = prompt('Cancellation reason (shown to passengers):') ?? ''
  if (!reason) return
  msg.value=''; err.value=''
  busy.value=true
  try{
    await http.post(`/trips/${selectedTripId.value}/cancel`, { reason })
    msg.value = 'Trip cancelled.'
    await loadTrips()
  }catch(e:any){ err.value = e?.response?.data?.message ?? e?.message ?? 'Failed.' }
  finally{ busy.value=false }
}

async function setBookingStatus(id:string, status:'Accepted'|'Rejected'){
  msg.value=''; err.value=''
  busy.value=true
  try{
    const reason = status==='Rejected' ? (prompt('Rejection reason (optional):') ?? null) : null
    await http.post(`/bookings/${id}/status/driver`, { status, reason })
    msg.value = `Booking ${status.toLowerCase()}.`
    await loadBookings()
  }catch(e:any){ err.value = e?.response?.data?.message ?? e?.message ?? 'Failed.' }
  finally{ busy.value=false }
}

async function openContact(id:string){
  busy.value=true
  try{
    const resp = await http.get(`/bookings/${id}/contact`)
    contact.value[id] = JSON.stringify(resp.data, null, 2)
  }finally{ busy.value=false }
}

loadTrips()
</script>
