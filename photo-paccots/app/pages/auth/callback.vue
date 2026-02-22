<script setup lang="ts">
/**
 * OAuth callback page — the redirect target after Microsoft login.
 *
 * With the redirect flow, MSAL's handleRedirectPromise() (called in the
 * plugin) processes the auth response automatically on any page load.
 * This page simply waits for that to complete, restores the session,
 * then redirects to the upload page (or home).
 */
definePageMeta({ layout: false })
useHead({ title: 'Signing in...' })

const { checkAuth, isAuthenticated } = useAuth()

onMounted(async () => {
  await checkAuth()
  navigateTo(isAuthenticated.value ? '/upload' : '/', { replace: true })
})
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-center bg-white text-center">
    <div class="h-10 w-10 animate-spin rounded-full border-4 border-stone-200 border-t-emerald-600" />
    <p class="mt-4 text-sm text-stone-500">Completing sign-in…</p>
  </div>
</template>
