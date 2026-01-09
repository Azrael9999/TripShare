<template>
  <div class="min-h-screen">
    <TopNav />
    <div v-if="mapsWarning" class="bg-amber-50 border-b border-amber-200 text-amber-900">
      <div class="mx-auto max-w-6xl px-4 py-2 text-sm">
        {{ mapsWarning }}
      </div>
    </div>
    <main class="mx-auto max-w-6xl px-4 pb-16 pt-6">
      <AdBlockNotice />
      <RouterView />
    </main>
    <FooterBar />
  </div>
</template>

<script setup lang="ts">
import TopNav from './components/TopNav.vue'
import FooterBar from './components/FooterBar.vue'
import AdBlockNotice from './components/AdBlockNotice.vue'
import { mapsWarning } from './lib/googleMaps'
</script>
