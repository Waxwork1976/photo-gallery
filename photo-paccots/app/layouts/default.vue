<script setup lang="ts">
const { checkAuth } = useAuth()

// Silently restore session on app mount (client-side only)
onMounted(() => {
  checkAuth()
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
