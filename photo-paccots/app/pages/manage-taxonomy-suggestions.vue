<script setup lang="ts">
import type { TaxonomySuggestion } from '~/types/taxonomySuggestion'
import ManageSubmenu from '~/components/manage/ManageSubmenu.vue'
import { useTranslations } from '~/composables/useTranslations'

definePageMeta({
  middleware: 'auth',
})

useHead({
  title: 'Manage Taxonomy Suggestions — Photo Paccots',
})

const api = useApi()
const { t, locale } = useTranslations()

const suggestions = ref<TaxonomySuggestion[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const actionInProgressId = ref<string | null>(null)

const fetchSuggestions = async () => {
  loading.value = true
  error.value = null
  try {
    const response = await api.listManageTaxonomySuggestions()
    suggestions.value = response.suggestions
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('taxonomyManage.loadError')} ${detail}`.trim()
  } finally {
    loading.value = false
  }
}

const formatDateTime = (iso: string) => {
  try {
    return new Date(iso).toLocaleString(locale.value, {
      dateStyle: 'medium',
      timeStyle: 'short',
    })
  } catch {
    return iso
  }
}

const removeSuggestionLocally = (id: string) => {
  suggestions.value = suggestions.value.filter(s => s.id !== id)
}

const acceptSuggestion = async (suggestion: TaxonomySuggestion) => {
  actionInProgressId.value = suggestion.id
  try {
    await api.acceptManageTaxonomySuggestion(suggestion.id)
    removeSuggestionLocally(suggestion.id)
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('taxonomyManage.acceptError')} ${detail}`.trim()
  } finally {
    actionInProgressId.value = null
  }
}

const rejectSuggestion = async (suggestion: TaxonomySuggestion) => {
  actionInProgressId.value = suggestion.id
  try {
    await api.rejectManageTaxonomySuggestion(suggestion.id)
    removeSuggestionLocally(suggestion.id)
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('taxonomyManage.rejectError')} ${detail}`.trim()
  } finally {
    actionInProgressId.value = null
  }
}

const mailtoHref = (suggestion: TaxonomySuggestion) => {
  const email = (suggestion.requester.email ?? '').trim()
  if (!email) return ''
  const subject = encodeURIComponent(`[Photo Paccots] ${t('taxonomyManage.mailSubject')}: ${suggestion.imageTitle || suggestion.imageId}`)
  const body = encodeURIComponent([
    `${t('taxonomyManage.mailBody.greeting')}`,
    '',
    `${t('taxonomyManage.mailBody.image')}: ${suggestion.imageTitle || suggestion.imageId}`,
    `${t('taxonomyManage.mailBody.current')}: ${suggestion.sourceTaxonomy.scientificName || '—'}`,
    `${t('taxonomyManage.mailBody.suggested')}: ${suggestion.suggestedTaxonomy.scientificName || '—'}`,
    `${t('taxonomyManage.mailBody.note')}: ${suggestion.note || '—'}`,
    '',
    `${t('taxonomyManage.mailBody.signature')}`,
  ].join('\n'))
  return `mailto:${encodeURIComponent(email)}?subject=${subject}&body=${body}`
}

onMounted(() => {
  fetchSuggestions()
})
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <ManageSubmenu />

    <section class="mb-8 flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
          {{ t('taxonomyManage.title') }}
        </h1>
        <p class="mt-1 text-sm text-stone-500">
          {{ t('taxonomyManage.subtitle', { count: suggestions.length }) }}
        </p>
      </div>
      <button class="btn-secondary" :disabled="loading" @click="fetchSuggestions">
        {{ t('taxonomyManage.refresh') }}
      </button>
    </section>

    <p v-if="loading" class="text-sm text-stone-500">
      {{ t('taxonomyManage.loading') }}
    </p>
    <p v-else-if="error" class="alert-error">
      {{ error }}
    </p>
    <p v-else-if="suggestions.length === 0" class="text-sm text-stone-500">
      {{ t('taxonomyManage.empty') }}
    </p>

    <section v-else class="space-y-4">
      <article
        v-for="suggestion in suggestions"
        :key="suggestion.id"
        class="ui-card p-4 sm:p-5"
      >
        <div class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 class="text-sm font-semibold text-stone-900">
              {{ suggestion.imageTitle || suggestion.imageId }}
            </h2>
            <p class="text-xs text-stone-500">
              {{ t('taxonomyManage.submittedAt') }}: {{ formatDateTime(suggestion.createdAt) }}
            </p>
          </div>
          <div class="text-xs text-stone-600">
            <p>{{ t('taxonomyManage.requester') }}: {{ suggestion.requester.name || '—' }}</p>
            <p>{{ t('taxonomyManage.email') }}: {{ suggestion.requester.email || '—' }}</p>
          </div>
        </div>

        <div class="mt-4 grid gap-3 sm:grid-cols-2">
          <div class="rounded-md border border-stone-200 bg-stone-50 p-3">
            <p class="text-xs font-semibold text-stone-700">{{ t('taxonomyManage.currentTaxonomy') }}</p>
            <p class="mt-1 text-xs text-stone-600">Order: {{ suggestion.sourceTaxonomy.order || '—' }}</p>
            <p class="text-xs text-stone-600">Family: {{ suggestion.sourceTaxonomy.family || '—' }}</p>
            <p class="text-xs text-stone-600">Genus: {{ suggestion.sourceTaxonomy.genus || '—' }}</p>
            <p class="text-xs text-stone-600">Scientific: {{ suggestion.sourceTaxonomy.scientificName || '—' }}</p>
            <p class="text-xs text-stone-600">Common: {{ suggestion.sourceTaxonomy.commonName || '—' }}</p>
          </div>
          <div class="rounded-md border border-emerald-200 bg-emerald-50 p-3">
            <p class="text-xs font-semibold text-emerald-800">{{ t('taxonomyManage.suggestedTaxonomy') }}</p>
            <p class="mt-1 text-xs text-emerald-700">Order: {{ suggestion.suggestedTaxonomy.order || '—' }}</p>
            <p class="text-xs text-emerald-700">Family: {{ suggestion.suggestedTaxonomy.family || '—' }}</p>
            <p class="text-xs text-emerald-700">Genus: {{ suggestion.suggestedTaxonomy.genus || '—' }}</p>
            <p class="text-xs text-emerald-700">Scientific: {{ suggestion.suggestedTaxonomy.scientificName || '—' }}</p>
            <p class="text-xs text-emerald-700">Common: {{ suggestion.suggestedTaxonomy.commonName || '—' }}</p>
          </div>
        </div>

        <div class="mt-3 rounded-md border border-stone-200 p-3">
          <p class="text-xs font-semibold text-stone-700">{{ t('taxonomyManage.note') }}</p>
          <p class="mt-1 text-sm text-stone-600">
            {{ suggestion.note || '—' }}
          </p>
        </div>

        <div class="mt-4 flex flex-wrap justify-end gap-2">
          <a
            v-if="suggestion.requester.email"
            class="btn-secondary"
            :href="mailtoHref(suggestion)"
          >
            {{ t('taxonomyManage.contactAuthor') }}
          </a>
          <button
            class="btn-subtle-danger"
            :disabled="actionInProgressId === suggestion.id"
            @click="rejectSuggestion(suggestion)"
          >
            {{ t('taxonomyManage.reject') }}
          </button>
          <button
            class="btn-primary"
            :disabled="actionInProgressId === suggestion.id"
            @click="acceptSuggestion(suggestion)"
          >
            {{ t('taxonomyManage.accept') }}
          </button>
        </div>
      </article>
    </section>
  </div>
</template>
