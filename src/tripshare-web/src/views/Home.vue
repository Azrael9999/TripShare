<template>
  <div class="space-y-8">
    <section class="relative overflow-hidden rounded-3xl bg-gradient-to-br from-brand-700 via-brand-600 to-purple-700 text-white shadow-soft">
      <div class="absolute inset-0 opacity-70" :style="heroBgStyle"></div>
      <div class="relative mx-auto max-w-6xl px-4 py-12 grid grid-cols-1 lg:grid-cols-12 gap-8 items-center">
        <div class="lg:col-span-6 space-y-4">
          <div class="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs uppercase tracking-wide">
            Community-powered · Riders manage their own payments
          </div>
          <h1 class="text-3xl sm:text-4xl font-semibold leading-tight">Plan smarter rides with live routes and verified drivers.</h1>
          <p class="text-white/80 max-w-2xl">
            Discover carpools, share your route, and book seats with confidence. New: live ETA refresh and shareable safety links. Payments and agreements happen directly between riders and drivers.
          </p>
          <div class="flex flex-wrap gap-3">
            <RouterLink to="/create" class="btn-primary-gradient">Create a trip</RouterLink>
            <button class="btn-outline bg-white/10 text-white border-white/30 hover:bg-white/20" @click="openLoginHint">
              Sign in to book
            </button>
          </div>
        </div>
        <div class="lg:col-span-6 flex justify-end">
          <div class="w-full max-w-lg bg-white/10 backdrop-blur rounded-3xl shadow-soft border border-white/20 p-4">
            <div class="card bg-white/90 shadow-soft border-white">
              <div class="p-5">
                <div class="text-slate-700 font-semibold mb-3">Find your next ride</div>
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
                  <button class="btn-primary-gradient" @click="search">Search</button>
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
            </div>
          </div>
        </div>
      </div>
    </section>

    <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
      <section class="lg:col-span-8">
        <div class="space-y-3">
          <div v-if="loading" class="space-y-3">
            <div v-for="i in 3" :key="i" class="card p-5 card-raise">
              <div class="flex items-start justify-between gap-4">
                <div class="flex-1 space-y-3">
                  <div class="skeleton h-4 w-40"></div>
                  <div class="skeleton h-6 w-3/4"></div>
                  <div class="skeleton h-3 w-1/2"></div>
                </div>
                <div class="w-20">
                  <div class="skeleton h-4 w-full"></div>
                  <div class="skeleton h-3 w-3/4 mt-2"></div>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="items.length === 0" class="card p-5">
            <div class="text-lg font-semibold">No trips found</div>
            <p class="text-slate-600 mt-1">Try a different search, or create a new trip.</p>
          </div>

          <RouterLink
            v-for="t in items"
            :key="t.id"
            :to="`/trips/${t.id}`"
            class="block card p-5 card-raise hover:border-brand-200"
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
            <RouterLink to="/create" class="btn-primary-gradient" v-if="auth.isAuthenticated && auth.me?.emailVerified">Create a trip</RouterLink>
            <RouterLink to="/profile" class="btn-outline" v-if="auth.isAuthenticated">Profile</RouterLink>
            <button class="btn-primary-gradient" v-if="!auth.isAuthenticated" @click="openLoginHint">Sign in</button>
          </div>
        </div>

        <div class="card p-0 overflow-hidden">
          <div class="relative h-52">
            <div class="absolute inset-0" :style="mapBgStyle"></div>
            <div class="absolute inset-0 bg-gradient-to-t from-slate-900/60 via-slate-900/20 to-transparent"></div>
            <div class="relative p-4 text-white flex flex-col h-full justify-end">
              <div class="text-sm uppercase tracking-wide text-white/70">Route preview</div>
              <div class="text-lg font-semibold">See pickups, drops, and live ETAs</div>
              <RouterLink to="/trips" class="btn-outline mt-3 border-white/40 text-white hover:bg-white/10">Explore trips</RouterLink>
            </div>
          </div>
        </div>

        <AdSlot name="home-sidebar" class="opacity-90" />
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import dayjs from 'dayjs'
import { http, TripListItem } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'
import { useAuthStore } from '../stores/auth'
import { brandConfig, hasImage } from '../lib/branding'
import { applySeo, upsertLdJson } from '../lib/seo'

const auth = useAuthStore()
const query = ref('')
const from = ref('')
const to = ref('')
const verifiedOnly = ref(false)
const minRating = ref<number | null>(null)
const maxPrice = ref<number | null>(null)

const loading = ref(false)
const items = ref<TripListItem[]>([])
const heroBgStyle = computed(() => {
  const url = hasImage(brandConfig.heroImageUrl) ? `url('${brandConfig.heroImageUrl}')` : ''
  return url
    ? { backgroundImage: `linear-gradient(120deg, rgba(37,99,235,0.7), rgba(124,58,237,0.5)), ${url}`, backgroundSize: 'cover', backgroundPosition: 'center' }
    : { backgroundImage: 'radial-gradient(circle at 20% 20%, rgba(255,255,255,0.15), transparent 35%), radial-gradient(circle at 80% 0%, rgba(255,255,255,0.15), transparent 25%)' }
})
const mapBgStyle = computed(() => {
  const url = hasImage(brandConfig.mapOverlayUrl) ? `url('${brandConfig.mapOverlayUrl}')` : ''
  return url
    ? { backgroundImage: `${url}`, backgroundSize: 'cover', backgroundPosition: 'center' }
    : { backgroundImage: 'linear-gradient(135deg, #0f172a, #1d4ed8)' }
})

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

onMounted(() => {
  applySeo({
    title: 'TripShare Sri Lanka | Carpool & Ride Sharing',
    description: 'Book or offer seats with verified drivers across Sri Lanka. Live ETAs, smart routes, and safety sharing for Colombo, Kandy, Galle and more.',
    image: brandConfig.heroImageUrl
  })

  upsertLdJson('home-breadcrumb', {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: [
      { '@type': 'ListItem', position: 1, name: 'Home', item: window.location.origin }
    ]
  })
})
</script>
