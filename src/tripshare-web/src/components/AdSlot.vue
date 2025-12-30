<template>
  <div v-if="ready && shouldRender" class="card p-4 shadow-none bg-slate-50/80 border-slate-100">
    <div class="text-xs uppercase tracking-wide text-slate-500 flex items-center gap-2">
      Sponsored
      <span v-if="!slot?.enabled" class="badge bg-amber-50 text-amber-700 border-amber-100">Paused</span>
    </div>
    <div v-if="slot" class="mt-3 text-sm text-slate-700" v-html="slot.html"></div>
    <div v-else class="mt-3 text-sm text-slate-500">Ad slot not configured.</div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAdsStore } from '../stores/ads'
import { useRoute } from 'vue-router'

const props = defineProps<{ name?: string; page?: string }>()
const ads = useAdsStore()
const ready = ref(false)
const route = useRoute()

const slotName = computed(() => props.name ?? 'default')
const pageKey = computed(() => props.page ?? (route.name?.toString() || route.path) ?? 'global')
const slot = computed(() => ads.slotConfig(slotName.value))
const shouldRender = computed(() => ads.canShow(slotName.value, pageKey.value))

onMounted(async () => {
  await ads.loadConfig()
  if (shouldRender.value) {
    await ads.recordImpression(slotName.value, pageKey.value)
  }
  ready.value = true
})
</script>
