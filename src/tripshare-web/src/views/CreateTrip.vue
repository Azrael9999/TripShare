<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-6">
      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">Create a trip</div>
            <p class="text-sm text-slate-600 mt-1">Add multiple stops and set per-segment pricing. Riders can book any section.</p>
          </div>
          <span class="badge">Email verified</span>
        </div>

        <div class="mt-5 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="text-sm text-slate-600">Departure (UTC)</label>
            <input v-model="departureUtc" type="datetime-local" class="input mt-1" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Seats</label>
            <input v-model.number="seats" type="number" min="1" max="8" class="input mt-1" />
          </div>

          <div>
            <label class="text-sm text-slate-600">Currency</label>
            <input v-model="currency" class="input mt-1" placeholder="LKR" />
          </div>
          <div class="flex items-end gap-3">
            <label class="chip">
              <input type="checkbox" v-model="instantBook" class="mr-2" />
              Instant book (no approval)
            </label>
          </div>

          <div>
            <label class="text-sm text-slate-600">Booking cutoff (minutes)</label>
            <input v-model.number="bookingCutoff" type="number" min="0" max="720" class="input mt-1" />
            <p class="text-xs text-slate-500 mt-1">Blocks new bookings close to departure.</p>
          </div>
          <div>
            <label class="text-sm text-slate-600">Pending booking expiry (minutes)</label>
            <input v-model.number="pendingExpiry" type="number" min="1" max="1440" class="input mt-1" />
            <p class="text-xs text-slate-500 mt-1">Auto-expires unapproved requests.</p>
          </div>

          <div class="sm:col-span-2">
            <label class="text-sm text-slate-600">Notes</label>
            <textarea v-model="notes" rows="3" class="textarea mt-1" placeholder="Pickup details, luggage rules, etc."></textarea>
          </div>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Route</div>
            <p class="text-sm text-slate-600 mt-1">Add the start, stops, and end. Order matters.</p>
          </div>
          <button class="btn-outline" @click="addStop"><PlusIcon class="h-5 w-5 mr-2" />Add stop</button>
        </div>

        <div class="mt-4 space-y-3">
          <RoutePointEditor
            v-for="(p, idx) in routePoints"
            :key="p.localId"
            :item="p"
            :canRemove="p.type==='Stop'"
            @remove="removeStop(idx)"
          />
        </div>

        <div v-if="routePoints.length < 2" class="mt-3 text-sm text-red-600">Route must have at least start and end.</div>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Segment pricing</div>
            <p class="text-sm text-slate-600 mt-1">Set a price per seat for each leg (A→B, B→C...).</p>
          </div>
        </div>

        <div class="mt-4 overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-600">
              <tr>
                <th class="py-2 pr-3">From</th>
                <th class="py-2 pr-3">To</th>
                <th class="py-2 pr-3 w-40">Price</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in segments" :key="s.key" class="border-t border-slate-100">
                <td class="py-3 pr-3 min-w-[240px]">{{ s.from.displayAddress || s.from.type }}</td>
                <td class="py-3 pr-3 min-w-[240px]">{{ s.to.displayAddress || s.to.type }}</td>
                <td class="py-3 pr-3">
                  <input v-model.number="s.price" type="number" min="0" step="1" class="input" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <p class="text-xs text-slate-500 mt-3">
          Riders booking a longer section will pay the sum of the included segments.
        </p>
      </div>

      <div class="flex items-center gap-3">
        <button class="btn-primary" :disabled="saving || !canSave" @click="save">
          <span v-if="!saving">Create trip</span>
          <span v-else>Creating...</span>
        </button>
        <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
        <p v-if="ok" class="text-sm text-emerald-700">Trip created. Redirecting...</p>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <div class="card p-5">
        <div class="font-semibold">Tip</div>
        <p class="text-sm text-slate-600 mt-2">
          Keep stops simple. Too many stops can confuse riders and reduce booking conversions.
        </p>
      </div>
      <AdSlot name="create-trip-sidebar" />
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { http } from '../lib/api'
import RoutePointEditor from '../components/RoutePointEditor.vue'
import AdSlot from '../components/AdSlot.vue'
import { PlusIcon } from '@heroicons/vue/24/outline'

type RP = {
  localId: string
  id?: string
  orderIndex: number
  type: 'Start' | 'Stop' | 'End'
  lat: number
  lng: number
  displayAddress: string
  placeId?: string
}

const router = useRouter()
const departureUtc = ref(new Date(Date.now() + 60 * 60 * 1000).toISOString().slice(0, 16))
const seats = ref(3)
const currency = ref('LKR')
const notes = ref('')
const instantBook = ref(false)
const bookingCutoff = ref(30)
const pendingExpiry = ref(30)

const routePoints = ref<RP[]>([
  { localId: crypto.randomUUID(), orderIndex: 0, type: 'Start', lat: 0, lng: 0, displayAddress: '' },
  { localId: crypto.randomUUID(), orderIndex: 1, type: 'End', lat: 0, lng: 0, displayAddress: '' }
])

function renumber() {
  routePoints.value.forEach((p, i) => (p.orderIndex = i))
}

function addStop() {
  const endIdx = routePoints.value.findIndex((x) => x.type === 'End')
  const idx = endIdx >= 0 ? endIdx : routePoints.value.length
  routePoints.value.splice(idx, 0, { localId: crypto.randomUUID(), orderIndex: idx, type: 'Stop', lat: 0, lng: 0, displayAddress: '' })
  renumber()
}
function removeStop(idx: number) {
  const p = routePoints.value[idx]
  if (p?.type !== 'Stop') return
  routePoints.value.splice(idx, 1)
  renumber()
}

const segments = computed(() => {
  const rp = routePoints.value.slice().sort((a, b) => a.orderIndex - b.orderIndex)
  const legs = []
  for (let i = 0; i < rp.length - 1; i++) {
    const from = rp[i]
    const to = rp[i + 1]
    legs.push({
      key: `${from.localId}->${to.localId}`,
      from,
      to,
      price: (segmentPrices.value.get(`${from.localId}|${to.localId}`) ?? 0) as number
    })
  }
  return legs
})

const segmentPrices = ref<Map<string, number>>(new Map())
const canSave = computed(() => {
  if (!departureUtc.value) return false
  if (routePoints.value.length < 2) return false
  if (!routePoints.value[0].displayAddress || !routePoints.value[routePoints.value.length - 1].displayAddress) return false
  return true
})

const saving = ref(false)
const error = ref('')
const ok = ref(false)

async function save() {
  error.value = ''
  ok.value = false
  saving.value = true
  try {
    // persist segment price map from computed legs
    for (const s of segments.value) {
      segmentPrices.value.set(`${s.from.localId}|${s.to.localId}`, s.price ?? 0)
    }

    const rp = routePoints.value.slice().sort((a, b) => a.orderIndex - b.orderIndex)
    const segReq = segments.value.map((s, i) => ({
      orderIndex: i,
      fromRoutePointOrderIndex: s.from.orderIndex,
      toRoutePointOrderIndex: s.to.orderIndex,
      price: Number(s.price ?? 0),
      currency: currency.value
    }))

    const resp = await http.post('/trips', {
      departureTimeUtc: new Date(departureUtc.value).toISOString(),
      seatsTotal: seats.value,
      currency: currency.value,
      notes: notes.value || null,
      instantBook: instantBook.value,
      bookingCutoffMinutes: bookingCutoff.value,
      pendingBookingExpiryMinutes: pendingExpiry.value,
      routePoints: rp.map((p) => ({
        orderIndex: p.orderIndex,
        type: p.type,
        lat: p.lat,
        lng: p.lng,
        displayAddress: p.displayAddress,
        placeId: p.placeId ?? null
      })),
      segmentPrices: segReq
    })
    ok.value = true
    const id = (resp.data?.id ?? resp.data?.Id) as string
    setTimeout(() => router.push(`/trips/${id}`), 650)
  } catch (e: any) {
    error.value = e?.response?.data?.message ?? e?.message ?? 'Failed to create trip.'
  } finally {
    saving.value = false
  }
}
</script>
