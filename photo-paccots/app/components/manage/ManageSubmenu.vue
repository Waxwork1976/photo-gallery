<template>
  <nav class="mb-6 flex flex-wrap gap-2">
    <NuxtLink
      to="/manage"
      class="rounded-lg px-3 py-1.5 text-sm font-medium transition-colors"
      active-class="bg-emerald-100 text-emerald-800"
      exact-active-class="bg-emerald-100 text-emerald-800"
      :class="$route.path === '/manage' ? '' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'"
    >
      {{ t('manage.submenu.images') }}
    </NuxtLink>
    <NuxtLink
      to="/manage-translations"
      class="rounded-lg px-3 py-1.5 text-sm font-medium transition-colors"
      active-class="bg-emerald-100 text-emerald-800"
      :class="$route.path === '/manage-translations' ? '' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'"
    >
      {{ t('manage.submenu.translations') }}
    </NuxtLink>
    <NuxtLink
      to="/manage-settings"
      class="rounded-lg px-3 py-1.5 text-sm font-medium transition-colors"
      active-class="bg-emerald-100 text-emerald-800"
      :class="$route.path === '/manage-settings' ? '' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'"
    >
      {{ t('manage.submenu.settings') }}
    </NuxtLink>
  </nav>
</template>

<script setup lang="ts">
import { useTranslations } from '~/composables/useTranslations'

const { t } = useTranslations()
</script>
