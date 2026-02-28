<script setup lang="ts">
import { useTranslations } from '~/composables/useTranslations'

const { t } = useTranslations()
</script>

<template>
  <footer class="border-t border-stone-200 bg-white">
    <div class="mx-auto flex max-w-7xl flex-col items-center gap-2 px-4 py-6 sm:px-6 lg:px-8">
      <p class="text-center text-sm text-stone-400">
        &copy; {{ new Date().getFullYear() }} Photo Paccots &mdash; Nature &amp; Garden Photography
        <span class="mx-1">|</span>
        <NuxtLink
          to="/powered-by"
          class="ui-focus-ring ui-transition-color rounded-md font-medium text-stone-500 hover:text-emerald-700"
        >
          {{ t('footer.thanksTo') }}
        </NuxtLink>
      </p>
    </div>
  </footer>
</template>
