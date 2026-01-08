<template>
  <header class="sticky top-0 z-30 bg-white/80 backdrop-blur border-b border-slate-100">
    <div class="mx-auto max-w-6xl px-4 py-3 flex items-center justify-between gap-3">
      <RouterLink to="/" class="flex items-center gap-3">
        <div class="h-10 w-10 rounded-2xl bg-gradient-to-br from-brand-600 to-brand-800 shadow-soft flex items-center justify-center overflow-hidden">
          <img v-if="brandLogo" :src="brandLogo" alt="HopTrip logo" class="h-full w-full object-cover" />
          <span v-else class="text-white font-semibold text-sm">HT</span>
        </div>
        <div class="leading-tight">
          <div class="font-semibold">HopTrip</div>
          <div class="text-xs text-slate-500">Carpool, simplified</div>
        </div>
      </RouterLink>

      <nav class="hidden md:flex items-center gap-1">
        <RouterLink to="/" class="btn-ghost">Explore</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified && auth.me?.phoneVerified" to="/create" class="btn-ghost">Create</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/my-trips" class="btn-ghost">My Trips</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified && auth.me?.phoneVerified" to="/bookings" class="btn-ghost">Bookings</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/notifications" class="btn-ghost">
          <span class="inline-flex items-center gap-2">
            <BellIcon class="h-5 w-5"/>
            <span>Alerts</span>
            <span v-if="unreadCount>0" class="ml-1 inline-flex items-center justify-center h-5 min-w-5 px-1 rounded-full bg-brand-600 text-white text-xs">{{ unreadCount }}</span>
          </span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/messages" class="btn-ghost">Messages</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/profile" class="btn-ghost">Profile</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/safety" class="btn-ghost">Safety</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.role==='admin'" to="/admin" class="btn-ghost">Admin</RouterLink>
      </nav>

      <div class="flex items-center gap-2">
        <RouterLink v-if="!auth.isAuthenticated" to="/sign-in" class="btn-primary">
          <ArrowRightOnRectangleIcon class="h-5 w-5 mr-2"/> Sign in
        </RouterLink>
        <RouterLink v-else to="/profile" class="btn-outline flex items-center gap-2">
          <img v-if="auth.me?.photoUrl" :src="auth.me?.photoUrl" class="h-7 w-7 rounded-full object-cover" />
          <UserCircleIcon v-else class="h-6 w-6 text-slate-500" />
          <span class="hidden sm:inline">{{ auth.me?.displayName }}</span>
        </RouterLink>
      </div>
    </div>

    <!-- Mobile nav -->
    <div class="md:hidden border-t border-slate-100 bg-white">
      <div class="mx-auto max-w-6xl px-4 py-2 flex items-center justify-between text-sm">
        <RouterLink to="/" class="btn-ghost px-3 py-2">Explore</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified && auth.me?.phoneVerified" to="/create" class="btn-ghost px-3 py-2">Create</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/notifications" class="btn-ghost px-3 py-2">Alerts</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/messages" class="btn-ghost px-3 py-2">Messages</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/profile" class="btn-ghost px-3 py-2">Profile</RouterLink>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { http } from '../lib/api'
import { brandConfig, hasImage } from '../lib/branding'
import {
  ArrowRightOnRectangleIcon,
  BellIcon,
  UserCircleIcon
} from '@heroicons/vue/24/outline'

const auth = useAuthStore()
const unreadCount = ref(0)
const brandLogo = computed(() => (hasImage(brandConfig.logoUrl) ? brandConfig.logoUrl : ''))

async function refreshUnread() {
  if (!auth.isAuthenticated) { unreadCount.value = 0; return }
  // If backend doesn't have a count endpoint, approximate by fetching unreadOnly=1
  try {
    const resp = await http.get<any[]>('/notifications', { params: { unreadOnly: true, take: 50 } })
    unreadCount.value = (resp.data ?? []).length
  } catch {
    unreadCount.value = 0
  }
}

onMounted(() => { refreshUnread() })
watch(() => auth.isAuthenticated, () => { refreshUnread() })
</script>
