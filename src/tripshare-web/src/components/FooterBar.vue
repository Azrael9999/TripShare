<template>
  <footer class="border-t border-slate-100 bg-white">
    <div class="mx-auto max-w-6xl px-4 py-8 text-sm text-slate-600 flex flex-col gap-4">
      <div class="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
        <div>© {{ new Date().getFullYear() }} HopTrip</div>
        <div class="flex gap-4">
          <a href="#" class="hover:text-brand-700">Privacy</a>
          <a href="#" class="hover:text-brand-700">Terms</a>
          <a href="#" class="hover:text-brand-700">Support</a>
        </div>
      </div>
      <div class="rounded-xl border border-amber-200 bg-amber-50 text-amber-800 px-3 py-2">
        HopTrip is a community ride coordination platform. All payments and ride responsibilities are handled directly between riders and drivers; HopTrip does not process payments or assume liability.
      </div>
    </div>
  </footer>
</template>
