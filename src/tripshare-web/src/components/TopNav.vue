<template>
  <header class="sticky top-0 z-30 bg-white/80 backdrop-blur border-b border-slate-100">
    <div class="mx-auto max-w-6xl px-4 py-3 flex items-center justify-between gap-3">
      <RouterLink to="/" class="flex items-center gap-3">
        <div class="h-9 w-9 rounded-2xl bg-gradient-to-br from-brand-600 to-brand-800 shadow-soft flex items-center justify-center">
          <span class="text-white font-semibold text-sm">TS</span>
        </div>
        <div class="leading-tight">
          <div class="font-semibold">TripShare</div>
          <div class="text-xs text-slate-500">Carpool, simplified</div>
        </div>
      </RouterLink>

      <nav class="hidden md:flex items-center gap-1">
        <RouterLink to="/" class="btn-ghost">Explore</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified" to="/create" class="btn-ghost">Create</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/my-trips" class="btn-ghost">My Trips</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified" to="/bookings" class="btn-ghost">Bookings</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/notifications" class="btn-ghost">
          <span class="inline-flex items-center gap-2">
            <BellIcon class="h-5 w-5"/>
            <span>Alerts</span>
            <span v-if="unreadCount>0" class="ml-1 inline-flex items-center justify-center h-5 min-w-5 px-1 rounded-full bg-brand-600 text-white text-xs">{{ unreadCount }}</span>
          </span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/profile" class="btn-ghost">Profile</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/safety" class="btn-ghost">Safety</RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.me?.role==='Admin'" to="/admin" class="btn-ghost">Admin</RouterLink>
      </nav>

      <div class="flex items-center gap-2">
        <button v-if="!auth.isAuthenticated" class="btn-primary" @click="openLogin=true">
          <ArrowRightOnRectangleIcon class="h-5 w-5 mr-2"/> Sign in
        </button>
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
        <RouterLink v-if="auth.isAuthenticated && auth.me?.emailVerified" to="/create" class="btn-ghost px-3 py-2">Create</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/notifications" class="btn-ghost px-3 py-2">Alerts</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/profile" class="btn-ghost px-3 py-2">Profile</RouterLink>
      </div>
    </div>

    <!-- Login modal -->
    <div v-if="openLogin" class="fixed inset-0 z-40 bg-slate-900/40 flex items-center justify-center p-4">
      <div class="card w-full max-w-md p-5">
        <div class="flex items-start justify-between">
          <div>
            <div class="text-lg font-semibold">Sign in</div>
            <p class="text-sm text-slate-600 mt-1">
              Trips can be created and booked only after email verification.
            </p>
          </div>
          <button class="btn-ghost" @click="openLogin=false"><XMarkIcon class="h-5 w-5"/></button>
        </div>
        <div class="mt-5">
          <GoogleSignIn @success="onGoogleSuccess" />
        </div>
        <p class="text-xs text-slate-500 mt-4">
          By continuing you agree to the Terms and Privacy Policy (placeholders).
        </p>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import GoogleSignIn from './GoogleSignIn.vue'
import { http } from '../lib/api'
import {
  ArrowRightOnRectangleIcon,
  BellIcon,
  UserCircleIcon,
  XMarkIcon
} from '@heroicons/vue/24/outline'

const auth = useAuthStore()
const openLogin = ref(false)
const unreadCount = ref(0)

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

async function onGoogleSuccess(idToken: string) {
  await auth.googleLogin(idToken)
  openLogin.value = false
  await refreshUnread()
}

onMounted(() => { refreshUnread() })
watch(() => auth.isAuthenticated, () => { refreshUnread() })
</script>
