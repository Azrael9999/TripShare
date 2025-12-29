<template>
  <div class="min-h-screen">
    <TopNav />
    <main class="mx-auto max-w-6xl px-4 pb-16 pt-6">
      <RouterView />
    </main>
    <FooterBar />
  </div>
</template>

<script setup lang="ts">
import TopNav from './components/TopNav.vue'
import FooterBar from './components/FooterBar.vue'
</script>
