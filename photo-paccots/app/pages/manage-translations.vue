<script setup lang="ts">
import type { SupportedLocale } from '~/types/translation'
import ManageSubmenu from '~/components/manage/ManageSubmenu.vue'
import { useTranslations } from '~/composables/useTranslations'

definePageMeta({
  middleware: 'auth',
})

useHead({
  title: 'Manage Translations — Photo Paccots',
})

const api = useApi()
const { t, defaultLocales, setRemoteLocales, supportedLocales } = useTranslations()

const selectedLocale = ref<SupportedLocale>('en')
const targetLocale = ref<SupportedLocale | 'all'>('all')
const loading = ref(true)
const saving = ref(false)
const updatingAll = ref(false)
const updatingByKey = ref<Record<string, boolean>>({})
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const search = ref('')
const draft = ref<Record<SupportedLocale, Record<string, string>>>({
  en: {},
  fr: {},
  de: {},
  it: {},
})

const orderedKeys = computed(() => {
  const allKeys = new Set<string>(Object.keys(defaultLocales.en))
  for (const locale of supportedLocales) {
    for (const key of Object.keys(draft.value[locale] ?? {})) {
      allKeys.add(key)
    }
  }
  return [...allKeys].sort((a, b) => a.localeCompare(b))
})

const filteredKeys = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return orderedKeys.value
  return orderedKeys.value.filter((k) => k.toLowerCase().includes(q))
})

const getValue = (key: string) => draft.value[selectedLocale.value]?.[key] ?? ''

const setValue = (key: string, value: string) => {
  draft.value[selectedLocale.value][key] = value
}

const normalizeLocales = (payload: unknown): Record<SupportedLocale, Record<string, string>> => {
  const candidate = payload as
    | { locales?: Partial<Record<SupportedLocale, Record<string, string>>>; Locales?: Partial<Record<SupportedLocale, Record<string, string>>> }
    | undefined
  const locales = candidate?.locales ?? candidate?.Locales
  return {
    en: { ...(locales?.en ?? {}) },
    fr: { ...(locales?.fr ?? {}) },
    de: { ...(locales?.de ?? {}) },
    it: { ...(locales?.it ?? {}) },
  }
}

const applyLocales = (locales: Record<SupportedLocale, Record<string, string>>) => {
  draft.value = {
    en: { ...defaultLocales.en, ...locales.en },
    fr: { ...defaultLocales.fr, ...locales.fr },
    de: { ...defaultLocales.de, ...locales.de },
    it: { ...defaultLocales.it, ...locales.it },
  }
}

const getTargetLocales = (): SupportedLocale[] | undefined => {
  if (targetLocale.value === 'all') return undefined
  if (targetLocale.value === selectedLocale.value) return undefined
  return [targetLocale.value]
}

const load = async () => {
  loading.value = true
  error.value = null
  success.value = null
  try {
    const res = await api.getManageTranslations()
    applyLocales(normalizeLocales(res))
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('translations.errorLoad')} ${detail}`
  } finally {
    loading.value = false
  }
}

const reset = async () => {
  await load()
}

const save = async () => {
  saving.value = true
  error.value = null
  success.value = null
  try {
    const res = await api.updateManageTranslations({ locales: draft.value })
    const locales = normalizeLocales(res)
    applyLocales(locales)
    setRemoteLocales(locales)
    success.value = t('translations.successSave')
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('translations.errorSave')} ${detail}`
  } finally {
    saving.value = false
  }
}

const updateAllFromSourceLanguage = async () => {
  updatingAll.value = true
  error.value = null
  success.value = null
  try {
    const res = await api.translateAllManageTranslations({
      sourceLocale: selectedLocale.value,
      targetLocales: getTargetLocales(),
      locales: draft.value,
    })
    applyLocales(normalizeLocales(res))
    success.value = t('translations.successUpdateAll')
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('translations.errorSave')} ${detail}`
  } finally {
    updatingAll.value = false
  }
}

const updateSingleKeyFromSourceLanguage = async (key: string) => {
  updatingByKey.value[key] = true
  error.value = null
  success.value = null
  try {
    const res = await api.translateKeyManageTranslations({
      sourceLocale: selectedLocale.value,
      targetLocales: getTargetLocales(),
      key,
      locales: draft.value,
    })
    applyLocales(normalizeLocales(res))
    success.value = t('translations.successUpdateOne')
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('translations.errorSave')} ${detail}`
  } finally {
    updatingByKey.value[key] = false
  }
}

watch(selectedLocale, () => {
  if (targetLocale.value === selectedLocale.value) {
    targetLocale.value = 'all'
  }
})

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <ManageSubmenu />

    <section class="mb-6">
      <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
        {{ t('translations.title') }}
      </h1>
      <p class="mt-1 text-sm text-stone-500">
        {{ t('translations.subtitle') }}
      </p>
    </section>

    <section class="rounded-xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="mb-4 grid gap-3 sm:grid-cols-[160px_160px_1fr_auto_auto_auto] sm:items-end">
        <div>
          <label class="block text-xs font-medium text-stone-600">{{ t('translations.language') }}</label>
          <select
            v-model="selectedLocale"
            class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          >
            <option v-for="lang in supportedLocales" :key="lang" :value="lang">
              {{ lang.toUpperCase() }}
            </option>
          </select>
        </div>
        <div>
          <label class="block text-xs font-medium text-stone-600">{{ t('translations.targetLanguage') }}</label>
          <select
            v-model="targetLocale"
            class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          >
            <option value="all">
              {{ t('translations.allLanguages') }}
            </option>
            <option v-for="lang in supportedLocales.filter((x) => x !== selectedLocale)" :key="lang" :value="lang">
              {{ lang.toUpperCase() }}
            </option>
          </select>
        </div>
        <div>
          <label class="block text-xs font-medium text-stone-600">{{ t('translations.search') }}</label>
          <input
            v-model="search"
            type="text"
            class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
            placeholder="upload.page.title"
          >
        </div>
        <button
          class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700 transition-colors disabled:opacity-50"
          :disabled="loading || saving || updatingAll"
          @click="updateAllFromSourceLanguage"
        >
          <span v-if="updatingAll">{{ t('translations.updatingAll') }}</span>
          <span v-else>{{ t('translations.updateAll') }}</span>
        </button>
        <button
          class="rounded-lg bg-stone-100 px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-200 transition-colors"
          :disabled="loading || saving || updatingAll"
          @click="reset"
        >
          {{ t('translations.reset') }}
        </button>
        <button
          class="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 transition-colors disabled:opacity-50"
          :disabled="loading || saving || updatingAll"
          @click="save"
        >
          <span v-if="saving">{{ t('manage.saving') }}</span>
          <span v-else>{{ t('translations.save') }}</span>
        </button>
      </div>

      <p v-if="loading" class="text-sm text-stone-500">{{ t('translations.loading') }}</p>
      <p v-if="error" class="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-600">{{ error }}</p>
      <p v-if="success" class="mb-3 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{{ success }}</p>

      <div v-if="!loading" class="space-y-2">
        <div
          v-for="key in filteredKeys"
          :key="key"
          class="grid gap-2 rounded-lg border border-stone-200 p-2 sm:grid-cols-[280px_1fr_auto]"
        >
          <div class="font-mono text-xs text-stone-500">
            {{ key }}
          </div>
          <input
            :value="getValue(key)"
            type="text"
            class="rounded-md border border-stone-300 px-2 py-1.5 text-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
            @input="setValue(key, ($event.target as HTMLInputElement).value)"
          >
          <button
            class="rounded-md bg-indigo-50 px-2 py-1.5 text-xs font-medium text-indigo-700 hover:bg-indigo-100 transition-colors disabled:opacity-50"
            :disabled="loading || saving || updatingAll || updatingByKey[key]"
            @click="updateSingleKeyFromSourceLanguage(key)"
          >
            <span v-if="updatingByKey[key]">{{ t('translations.updatingOne') }}</span>
            <span v-else>{{ t('translations.updateOne') }}</span>
          </button>
        </div>
      </div>
    </section>
  </div>
</template>
