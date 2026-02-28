<script setup lang="ts">
import { ArrowTopRightOnSquareIcon } from '@heroicons/vue/24/outline'
import { useTranslations } from '~/composables/useTranslations'

useHead({
  title: 'Powered by — Photo Paccots',
})

const { t } = useTranslations()

const toBase64 = (value: string) => {
  if (import.meta.server) {
    const bufferApi = (globalThis as { Buffer?: { from: (input: string, encoding: string) => { toString: (encoding: string) => string } } }).Buffer
    if (bufferApi) {
      return bufferApi.from(value, 'utf-8').toString('base64')
    }
  }
  return btoa(value)
}

const asEmbeddedLogo = (label: string, background: string, foreground: string) => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96"><rect width="96" height="96" rx="20" fill="${background}"/><text x="48" y="56" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" font-weight="700" fill="${foreground}">${label}</text></svg>`
  return `data:image/svg+xml;base64,${toBase64(svg)}`
}

const externalApis = [
  {
    name: 'Pl@ntNet API',
    url: 'https://my-api.plantnet.org',
    description: 'Plant identification from uploaded photos.',
    logoDataUrl: asEmbeddedLogo('PN', '#DCFCE7', '#166534'),
  },
  {
    name: 'RapidAPI Bird Classifier',
    url: 'https://rapidapi.com',
    description: 'Bird species identification service.',
    logoDataUrl: asEmbeddedLogo('RA', '#FEF3C7', '#92400E'),
  },
]
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6">
    <section>
      <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
        {{ t('poweredBy.title') }}
      </h1>
      <p class="mt-1 text-sm text-stone-500">
        {{ t('poweredBy.subtitle') }}
      </p>
    </section>

    <section class="ui-card p-5 sm:p-6">
      <h2 class="text-sm font-semibold uppercase tracking-wide text-stone-600">
        {{ t('poweredBy.externalApis') }}
      </h2>
      <ul class="mt-3 space-y-2">
        <li
          v-for="api in externalApis"
          :key="api.url"
          class="flex items-center justify-between gap-3 rounded-md border border-stone-200 px-3 py-2"
        >
          <div class="flex min-w-0 items-center gap-3">
            <img :src="api.logoDataUrl" :alt="`${api.name} logo`" class="h-9 w-9 rounded-md border border-stone-200 bg-white object-contain p-1">
            <div class="min-w-0">
              <p class="font-medium text-stone-900">{{ api.name }}</p>
              <p class="text-xs text-stone-500">{{ api.description }}</p>
            </div>
          </div>
          <a
            :href="api.url"
            target="_blank"
            rel="noopener noreferrer"
            class="ui-focus-ring ui-transition-color inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-emerald-700 hover:bg-emerald-50"
          >
            Open
            <ArrowTopRightOnSquareIcon class="h-3.5 w-3.5" />
          </a>
        </li>
      </ul>
    </section>
  </div>
</template>
