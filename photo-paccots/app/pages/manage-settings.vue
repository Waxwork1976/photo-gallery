<script setup lang="ts">
import ManageSubmenu from '~/components/manage/ManageSubmenu.vue'
import { useTranslations } from '~/composables/useTranslations'

definePageMeta({
  middleware: 'auth',
})

useHead({
  title: 'Manage Settings — Photo Paccots',
})

const api = useApi()
const { t } = useTranslations()

const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)

const photoCount = ref(8)
const intervalSeconds = ref(4)
const transitionSeconds = ref(0.8)

const load = async () => {
  loading.value = true
  error.value = null
  success.value = null
  try {
    const settings = await api.getManageSlideshowSettings()
    photoCount.value = settings.photoCount
    intervalSeconds.value = settings.intervalSeconds
    transitionSeconds.value = settings.transitionSeconds
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('settings.loadError')} ${detail}`
  } finally {
    loading.value = false
  }
}

const save = async () => {
  saving.value = true
  error.value = null
  success.value = null

  const normalizedInterval = Math.max(1, Math.floor(intervalSeconds.value || 1))
  const normalizedTransition = Math.max(0, Number(transitionSeconds.value || 0))

  const payload = {
    photoCount: Math.max(1, Math.floor(photoCount.value || 1)),
    intervalSeconds: normalizedInterval,
    transitionSeconds: Math.min(normalizedTransition, normalizedInterval),
  }

  try {
    const settings = await api.updateManageSlideshowSettings(payload)
    photoCount.value = settings.photoCount
    intervalSeconds.value = settings.intervalSeconds
    transitionSeconds.value = settings.transitionSeconds
    success.value = t('settings.saveSuccess')
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('settings.saveError')} ${detail}`
  } finally {
    saving.value = false
  }
}

onMounted(load)

watch(intervalSeconds, (nextInterval) => {
  const safeInterval = Math.max(1, Number(nextInterval || 1))
  if (transitionSeconds.value > safeInterval) {
    transitionSeconds.value = safeInterval
  }
})
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <ManageSubmenu />

    <section class="mb-6">
      <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
        {{ t('settings.title') }}
      </h1>
      <p class="mt-1 text-sm text-stone-500">
        {{ t('settings.subtitle') }}
      </p>
    </section>

    <section class="rounded-xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
      <p v-if="loading" class="text-sm text-stone-500">
        {{ t('manage.loading') }}
      </p>
      <p v-if="error" class="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-600">
        {{ error }}
      </p>
      <p v-if="success" class="mb-3 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
        {{ success }}
      </p>

      <div v-if="!loading" class="grid gap-4 sm:grid-cols-3">
        <label class="block">
          <span class="block text-xs font-medium text-stone-600">{{ t('settings.photoCount') }}</span>
          <input
            v-model.number="photoCount"
            type="number"
            min="1"
            class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          >
        </label>

        <label class="block">
          <span class="block text-xs font-medium text-stone-600">{{ t('settings.intervalSeconds') }}</span>
          <input
            v-model.number="intervalSeconds"
            type="number"
            min="1"
            class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          >
        </label>

        <label class="block">
          <span class="block text-xs font-medium text-stone-600">{{ t('settings.transitionSeconds') }}</span>
          <input
            v-model.number="transitionSeconds"
            type="number"
            min="0"
            step="0.1"
            :max="Math.max(1, intervalSeconds || 1)"
            class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          >
        </label>
      </div>

      <div class="mt-5 flex justify-end">
        <button
          class="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-emerald-700 disabled:opacity-50"
          :disabled="loading || saving"
          @click="save"
        >
          <span v-if="saving">{{ t('manage.saving') }}</span>
          <span v-else>{{ t('settings.save') }}</span>
        </button>
      </div>
    </section>
  </div>
</template>
