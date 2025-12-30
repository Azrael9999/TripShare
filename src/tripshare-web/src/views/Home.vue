<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8">
      <div class="card p-5">
        <div class="flex flex-col sm:flex-row sm:items-end gap-3">
          <div class="flex-1">
            <label class="text-sm text-slate-600">Search</label>
            <input v-model="query" class="input mt-1" placeholder="City, landmark, route..." />
          </div>
          <div class="w-full sm:w-56">
            <label class="text-sm text-slate-600">From (UTC)</label>
            <input v-model="from" type="datetime-local" class="input mt-1" />
          </div>
          <div class="w-full sm:w-56">
            <label class="text-sm text-slate-600">To (UTC)</label>
            <input v-model="to" type="datetime-local" class="input mt-1" />
          </div>
          <button class="btn-primary" @click="search">Search</button>
        </div>

        <div class="mt-4 flex flex-col sm:flex-row gap-3 sm:items-center">
          <label class="chip">
            <input type="checkbox" v-model="verifiedOnly" class="mr-2" />
            Verified drivers only
          </label>
          <div class="flex-1"></div>
          <div class="w-full sm:w-56">
            <label class="text-xs text-slate-600">Min driver rating</label>
            <input v-model.number="minRating" type="number" step="0.1" min="0" max="5" class="input mt-1" />
          </div>
          <div class="w-full sm:w-56">
            <label class="text-xs text-slate-600">Max price per seat</label>
            <input v-model.number="maxPrice" type="number" step="1" min="0" class="input mt-1" />
          </div>
        </div>
      </div>

      <div class="mt-6 space-y-3">
        <div v-if="loading" class="card p-5 text-slate-600">Loading trips...</div>

        <div v-else-if="items.length === 0" class="card p-5">
          <div class="text-lg font-semibold">No trips found</div>
          <p class="text-slate-600 mt-1">Try a different search, or create a new trip.</p>
        </div>

        <RouterLink
          v-for="t in items"
          :key="t.id"
          :to="`/trips/${t.id}`"
          class="block card p-5 hover:border-brand-200 transition"
        >
          <div class="flex items-start justify-between gap-4">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <span class="badge">{{ dayjs(t.departureTimeUtc).format('MMM D, YYYY HH:mm') }} UTC</span>
                <span class="text-sm text-slate-500">{{ t.seatsTotal }} seats</span>
                <span v-if="t.instantBook" class="chip">Instant book</span>
                <span v-if="t.driverVerified || t.driverIdentityVerified" class="chip bg-emerald-50 text-emerald-700 border-emerald-100">
                  Verified driver
                </span>
              </div>
              <div class="mt-3 font-semibold text-lg truncate">
                {{ startAddr(t) }} → {{ endAddr(t) }}
              </div>
              <div class="mt-2 text-sm text-slate-600 truncate">
                Driver: {{ t.driverName || 'Driver' }}
              </div>
            </div>
            <div class="text-right">
              <div class="text-sm text-slate-500">From</div>
              <div class="text-xl font-semibold text-brand-700">{{ minSegmentPrice(t) }}</div>
              <div class="text-xs text-slate-500 mt-1">per seat (min)</div>
            </div>
          </div>
        </RouterLink>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <div class="card p-5">
        <div class="font-semibold">Quick actions</div>
        <p class="text-sm text-slate-600 mt-1">Create trips and manage bookings after verifying your email.</p>
        <div class="mt-4 flex flex-col gap-2">
          <RouterLink to="/create" class="btn-primary" v-if="auth.isAuthenticated && auth.me?.emailVerified">Create a trip</RouterLink>
          <RouterLink to="/profile" class="btn-outline" v-if="auth.isAuthenticated">Profile</RouterLink>
          <button class="btn-primary" v-if="!auth.isAuthenticated" @click="openLoginHint">Sign in</button>
        </div>
      </div>

      <AdSlot name="home-sidebar" />
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import dayjs from 'dayjs'
import { http, TripListItem } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const query = ref('')
const from = ref('')
const to = ref('')
const verifiedOnly = ref(false)
const minRating = ref<number | null>(null)
const maxPrice = ref<number | null>(null)

const loading = ref(false)
const items = ref<TripListItem[]>([])

function startAddr(t: TripListItem) {
  return t.routePoints?.[0]?.displayAddress ?? 'Start'
}
function endAddr(t: TripListItem) {
  const rp = t.routePoints ?? []
  return rp.length ? rp[rp.length - 1].displayAddress : 'End'
}
function minSegmentPrice(t: TripListItem) {
  const prices = (t.segments ?? []).map(x => x.price)
  if (!prices.length) return `0 ${t.currency ?? ''}`.trim()
  const min = Math.min(...prices)
  const currency = t.currency ?? t.segments?.[0]?.currency ?? ''
  return `${min.toFixed(0)} ${currency}`.trim()
}

async function search() {
  loading.value = true
  try {
    const resp = await http.post<any>('/trips/search', {
      query: query.value || null,
      fromUtc: from.value ? new Date(from.value).toISOString() : null,
      toUtc: to.value ? new Date(to.value).toISOString() : null,
      maxPricePerSeat: maxPrice.value ?? null,
      minDriverRating: minRating.value ?? null,
      verifiedDriversOnly: verifiedOnly.value,
      page: 1,
      pageSize: 20
    })
    items.value = resp.data.items ?? resp.data.Items ?? []
  } finally {
    loading.value = false
  }
}

function openLoginHint() {
  // The TopNav shows a login modal; hint users with query parameter.
  window.location.href = '/?login=1'
}

search()
</script>
