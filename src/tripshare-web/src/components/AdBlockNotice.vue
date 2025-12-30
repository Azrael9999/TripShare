<template>
  <div v-if="show" class="mb-4 rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 shadow-sm">
    <div class="flex items-start gap-3">
      <div class="mt-0.5 h-2.5 w-2.5 rounded-full bg-amber-500"></div>
      <div class="flex-1 space-y-2">
        <div class="font-semibold text-amber-900">Please consider allowing ads</div>
        <p class="text-sm text-amber-900/90">
          Ads keep HopTrip running. If you trust us, please disable your ad blocker for this site.
          We keep ads light and never use popups.
        </p>
        <div class="flex flex-wrap gap-2">
          <button class="btn-outline" type="button" @click="dismiss">Maybe later</button>
          <button class="btn-primary" type="button" @click="acknowledgeDisabled">I disabled it</button>
          <button class="btn-ghost" type="button" @click="reduceAds">Reduce ads</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useAdsStore } from '../stores/ads'

const show = ref(false)
const DISMISS_KEY = 'tripshare.adblock.dismissed'
const ads = useAdsStore()

function dismiss() {
  localStorage.setItem(DISMISS_KEY, '1')
  show.value = false
}

function acknowledgeDisabled() {
  localStorage.setItem(DISMISS_KEY, '1')
  show.value = false
}

function reduceAds() {
  ads.setUserReduced(true)
  dismiss()
}

function detectAdBlock() {
  if (localStorage.getItem(DISMISS_KEY) === '1') return

  const bait = document.createElement('div')
  bait.className = 'adsbox pub_300x250 adsbygoogle ad-banner'
  bait.style.position = 'absolute'
  bait.style.left = '-999px'
  bait.style.width = '300px'
  bait.style.height = '250px'
  document.body.appendChild(bait)

  window.setTimeout(() => {
    const blocked =
      bait.offsetParent === null ||
      bait.clientHeight === 0 ||
      bait.clientWidth === 0 ||
      getComputedStyle(bait).display === 'none' ||
      getComputedStyle(bait).visibility === 'hidden'

    if (blocked) {
      show.value = true
    }
    bait.remove()
  }, 100)
}

onMounted(() => detectAdBlock())
</script>
