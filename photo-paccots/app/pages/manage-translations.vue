<script setup lang="ts">
import { LockClosedIcon, LockOpenIcon } from '@heroicons/vue/24/outline'
import type { SupportedLocale, TranslationEntry, TranslationEntryDictionary } from '~/types/translation'
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
const draft = ref<Record<SupportedLocale, TranslationEntryDictionary>>({
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

const toEntry = (value: string = '', autoTranslate = true): TranslationEntry => ({
  value,
  autoTranslate,
})

const ensureEntry = (locale: SupportedLocale, key: string): TranslationEntry => {
  const existing = draft.value[locale][key]
  if (existing) return existing
  const seeded = toEntry(defaultLocales[locale]?.[key] ?? '', true)
  draft.value[locale][key] = seeded
  return seeded
}

const getValue = (key: string) => ensureEntry(selectedLocale.value, key).value
const isAutoTranslate = (key: string) => ensureEntry(selectedLocale.value, key).autoTranslate

const setValue = (key: string, value: string) => {
  const entry = ensureEntry(selectedLocale.value, key)
  entry.value = value
  entry.autoTranslate = false
}

const toggleAutoTranslate = (key: string) => {
  const entry = ensureEntry(selectedLocale.value, key)
  entry.autoTranslate = !entry.autoTranslate
}

const normalizeEntryDictionary = (dictionary: unknown): TranslationEntryDictionary => {
  const candidate = dictionary as Record<string, unknown> | undefined
  const normalized: TranslationEntryDictionary = {}
  if (!candidate) return normalized

  for (const [key, value] of Object.entries(candidate)) {
    if (!key?.trim()) continue
    if (typeof value === 'string') {
      normalized[key] = toEntry(value, true)
      continue
    }
    if (value && typeof value === 'object') {
      const obj = value as { value?: unknown; Value?: unknown; autoTranslate?: unknown; AutoTranslate?: unknown }
      const autoTranslateRaw = obj.autoTranslate ?? obj.AutoTranslate
      const autoTranslate = typeof autoTranslateRaw === 'boolean'
        ? autoTranslateRaw
        : true
      normalized[key] = toEntry(
        String(obj.value ?? obj.Value ?? ''),
        autoTranslate,
      )
    }
  }
  return normalized
}

const normalizeLocales = (payload: unknown): Record<SupportedLocale, TranslationEntryDictionary> => {
  const candidate = payload as
    | { locales?: Partial<Record<SupportedLocale, unknown>>; Locales?: Partial<Record<SupportedLocale, unknown>> }
    | undefined
  const locales = candidate?.locales ?? candidate?.Locales
  return {
    en: normalizeEntryDictionary(locales?.en),
    fr: normalizeEntryDictionary(locales?.fr),
    de: normalizeEntryDictionary(locales?.de),
    it: normalizeEntryDictionary(locales?.it),
  }
}

const toDefaultEntries = (locale: SupportedLocale): TranslationEntryDictionary => {
  const entries: TranslationEntryDictionary = {}
  for (const [key, value] of Object.entries(defaultLocales[locale] ?? {})) {
    entries[key] = toEntry(value, true)
  }
  return entries
}

const applyLocales = (locales: Record<SupportedLocale, TranslationEntryDictionary>) => {
  draft.value = {
    en: { ...toDefaultEntries('en'), ...locales.en },
    fr: { ...toDefaultEntries('fr'), ...locales.fr },
    de: { ...toDefaultEntries('de'), ...locales.de },
    it: { ...toDefaultEntries('it'), ...locales.it },
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
    setRemoteLocales({
      en: Object.fromEntries(Object.entries(locales.en).map(([key, entry]) => [key, entry.value])),
      fr: Object.fromEntries(Object.entries(locales.fr).map(([key, entry]) => [key, entry.value])),
      de: Object.fromEntries(Object.entries(locales.de).map(([key, entry]) => [key, entry.value])),
      it: Object.fromEntries(Object.entries(locales.it).map(([key, entry]) => [key, entry.value])),
    })
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

    <section class="ui-card p-4 sm:p-6">
      <div class="mb-4 grid gap-3 sm:grid-cols-[160px_160px_1fr_auto_auto_auto] sm:items-end">
        <div>
          <label class="block text-xs font-medium text-stone-600">{{ t('translations.language') }}</label>
          <select
            v-model="selectedLocale"
            class="ui-input-compact"
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
            class="ui-input-compact"
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
            class="ui-input-compact"
            placeholder="upload.page.title"
          >
        </div>
        <button
          class="btn-accent"
          :disabled="loading || saving || updatingAll"
          @click="updateAllFromSourceLanguage"
        >
          <span v-if="updatingAll">{{ t('translations.updatingAll') }}</span>
          <span v-else>{{ t('translations.updateAll') }}</span>
        </button>
        <button
          class="btn-secondary"
          :disabled="loading || saving || updatingAll"
          @click="reset"
        >
          {{ t('translations.reset') }}
        </button>
        <button
          class="btn-primary"
          :disabled="loading || saving || updatingAll"
          @click="save"
        >
          <span v-if="saving">{{ t('manage.saving') }}</span>
          <span v-else>{{ t('translations.save') }}</span>
        </button>
      </div>

      <p v-if="loading" class="text-sm text-stone-500">{{ t('translations.loading') }}</p>
      <p v-if="error" class="alert-error">{{ error }}</p>
      <p v-if="success" class="alert-success">{{ success }}</p>

      <div v-if="!loading" class="space-y-2">
        <div
          v-for="key in filteredKeys"
          :key="key"
          class="grid gap-2 rounded-lg border border-stone-200 p-2 sm:grid-cols-[280px_1fr_auto_auto]"
        >
          <div class="font-mono text-xs text-stone-500">
            {{ key }}
          </div>
          <input
            :value="getValue(key)"
            type="text"
            class="ui-input-compact mt-0"
            @input="setValue(key, ($event.target as HTMLInputElement).value)"
          >
          <button
            class="btn-secondary rounded-md px-2 py-1.5 text-xs"
            :class="isAutoTranslate(key) ? '!border-emerald-200 !bg-emerald-50 !text-emerald-700 hover:!bg-emerald-100' : '!border-red-200 !bg-red-50 !text-red-700 hover:!bg-red-100'"
            :title="isAutoTranslate(key) ? t('translations.autoTranslateOn') : t('translations.autoTranslateOff')"
            :disabled="loading || saving || updatingAll"
            @click="toggleAutoTranslate(key)"
          >
            <LockOpenIcon v-if="isAutoTranslate(key)" class="h-4 w-4 text-emerald-700" />
            <LockClosedIcon v-else class="h-4 w-4 text-red-700" />
          </button>
          <button
            class="btn-accent-soft rounded-md px-2 py-1.5 text-xs"
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
