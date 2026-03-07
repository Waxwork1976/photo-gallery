<template>
  <nav class="mb-6 flex flex-wrap items-center gap-2 sm:flex-nowrap sm:overflow-x-auto sm:pb-1">
    <NuxtLink
      to="/manage"
      class="ui-focus-ring ui-transition-color inline-flex items-center gap-1.5 rounded-lg px-4 py-1.5 text-sm font-medium sm:whitespace-nowrap"
      active-class="bg-emerald-100 text-emerald-800"
      exact-active-class="bg-emerald-100 text-emerald-800"
      :class="$route.path === '/manage' ? '' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'"
    >
      <PhotoIcon class="h-4 w-4" />
      {{ t('manage.submenu.images') }}
    </NuxtLink>
    <NuxtLink
      to="/manage-translations"
      class="ui-focus-ring ui-transition-color inline-flex items-center gap-1.5 rounded-lg px-4 py-1.5 text-sm font-medium sm:whitespace-nowrap"
      active-class="bg-emerald-100 text-emerald-800"
      :class="$route.path === '/manage-translations' ? '' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'"
    >
      <LanguageIcon class="h-4 w-4" />
      {{ t('manage.submenu.translations') }}
    </NuxtLink>
    <NuxtLink
      to="/manage-taxonomy-suggestions"
      class="ui-focus-ring ui-transition-color inline-flex items-center gap-2 rounded-lg px-4 py-1.5 text-sm font-medium sm:whitespace-nowrap"
      :class="taxonomySuggestionLinkClass"
    >
      <ClipboardDocumentListIcon class="h-4 w-4" />
      {{ t('manage.submenu.taxonomySuggestions') }}
      <span
        v-if="pendingSuggestionCount > 0"
        class="inline-flex min-w-5 items-center justify-center rounded-full bg-amber-600 px-1.5 text-[10px] font-semibold leading-4 text-white"
      >
        {{ pendingSuggestionCount }}
      </span>
    </NuxtLink>
    <NuxtLink
      to="/manage-settings"
      class="ui-focus-ring ui-transition-color inline-flex items-center gap-1.5 rounded-lg px-4 py-1.5 text-sm font-medium sm:whitespace-nowrap"
      active-class="bg-emerald-100 text-emerald-800"
      :class="$route.path === '/manage-settings' ? '' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'"
    >
      <Cog6ToothIcon class="h-4 w-4" />
      {{ t('manage.submenu.settings') }}
    </NuxtLink>
  </nav>
</template>

<script setup lang="ts">
import { ClipboardDocumentListIcon, Cog6ToothIcon, LanguageIcon, PhotoIcon } from '@heroicons/vue/24/outline'
import { useTranslations } from '~/composables/useTranslations'

const { t } = useTranslations()
const api = useApi()
const route = useRoute()
const pendingSuggestionCount = ref(0)
let refreshTimer: ReturnType<typeof setInterval> | null = null

const loadPendingSuggestionCount = async () => {
  try {
    const response = await api.getManageTaxonomySuggestionCount()
    pendingSuggestionCount.value = Math.max(0, response.pendingCount || 0)
  } catch {
    pendingSuggestionCount.value = 0
  }
}

const taxonomySuggestionLinkClass = computed(() => {
  const isActive = route.path === '/manage-taxonomy-suggestions'
  if (pendingSuggestionCount.value > 0) {
    return isActive
      ? 'bg-amber-100 text-amber-900 ring-1 ring-amber-300'
      : 'bg-amber-50 text-amber-800 ring-1 ring-amber-200 hover:bg-amber-100'
  }
  return isActive
    ? 'bg-emerald-100 text-emerald-800'
    : 'bg-stone-100 text-stone-700 hover:bg-stone-200'
})

onMounted(async () => {
  await loadPendingSuggestionCount()
  refreshTimer = setInterval(() => {
    loadPendingSuggestionCount()
  }, 15000)
})

watch(() => route.fullPath, () => {
  loadPendingSuggestionCount()
})

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer)
})
</script>
