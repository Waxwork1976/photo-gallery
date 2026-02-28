<script setup lang="ts">
import { useTranslations } from '~/composables/useTranslations'

const { checkAuth } = useAuth()
const { initLocale, loadTranslations } = useTranslations()

// Silently restore session on app mount (client-side only)
onMounted(() => {
  checkAuth()
  initLocale()
  loadTranslations()
})
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <LayoutHeader />

    <main class="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
      <slot />
    </main>

    <LayoutFooter />
  </div>
</template>
