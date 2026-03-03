<script setup lang="ts">
import type { ImageDto } from '~/types/image'
import { useTranslations } from '~/composables/useTranslations'

const props = defineProps<{
  image: ImageDto
  showSuggestButton?: boolean
}>()

const emit = defineEmits<{
  click: [image: ImageDto]
  suggestTaxonomy: [image: ImageDto]
}>()

const loaded = ref(false)
const { locale, t } = useTranslations()

/** Human-readable file size. */
const formattedSize = computed(() => {
  const bytes = props.image.sizeBytes
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
})

const localizedCommonName = computed(() => {
  const commonNames = props.image.commonNames ?? {}
  return commonNames[locale.value] || commonNames.en || props.image.scientificName || ''
})
</script>

<template>
  <article
    class="ui-focus-ring group cursor-pointer overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-stone-200 transition-all duration-300 ease-out hover:shadow-lg hover:ring-emerald-300 motion-reduce:transition-none"
    tabindex="0"
    @click="emit('click', image)"
    @keydown.enter.prevent="emit('click', image)"
    @keydown.space.prevent="emit('click', image)"
  >
    <!-- Image wrapper with aspect-ratio -->
    <div class="relative aspect-[4/3] overflow-hidden bg-stone-100">
      <!-- Skeleton loader -->
      <div
        v-if="!loaded"
        class="absolute inset-0 animate-pulse bg-gradient-to-br from-stone-200 to-stone-100"
      />

      <img
        :src="image.thumbnailUrl"
        :alt="image.title || 'Gallery photo'"
        :width="image.width || undefined"
        :height="image.height || undefined"
        loading="lazy"
        class="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-105 motion-reduce:transition-none motion-reduce:transform-none"
        :class="{ 'opacity-0': !loaded }"
        @load="loaded = true"
      >

      <!-- Featured badge -->
      <span
        v-if="image.featured"
        class="absolute left-2 top-2 rounded-full bg-amber-400 px-2 py-0.5 text-xs font-bold uppercase tracking-wide text-amber-900 shadow"
      >
        Featured
      </span>
    </div>

    <!-- Card body -->
    <div class="px-4 py-3">
      <h3 class="truncate text-sm font-semibold text-stone-800 group-hover:text-emerald-700 transition-colors">
        {{ image.title || localizedCommonName || 'Untitled' }}
      </h3>

      <p v-if="localizedCommonName || image.description" class="mt-0.5 line-clamp-2 text-xs text-stone-500">
        <span v-if="localizedCommonName">{{ localizedCommonName }}</span>
        <span v-if="localizedCommonName && image.description"> — </span>
        <span v-if="image.description">{{ image.description }}</span>
      </p>

      <div class="mt-2 flex flex-wrap items-center gap-1.5">
        <span
          v-for="tag in image.tags.slice(0, 4)"
          :key="tag"
          class="max-w-full truncate rounded-full bg-stone-100 px-2 py-0.5 text-xs font-medium text-stone-600"
        >
          {{ tag }}
        </span>
        <span v-if="image.tags.length > 4" class="text-xs text-stone-500">
          +{{ image.tags.length - 4 }}
        </span>
      </div>

      <div class="mt-2 flex min-w-0 items-center justify-between gap-2 text-xs text-stone-500">
        <time class="truncate" :datetime="image.uploadedAt">
          {{ new Date(image.uploadedAt).toLocaleDateString() }}
        </time>
        <span class="shrink-0">{{ formattedSize }}</span>
      </div>

      <div v-if="showSuggestButton" class="mt-3">
        <button
          type="button"
          class="btn-secondary w-full rounded-md py-1.5 text-xs"
          @click.stop="emit('suggestTaxonomy', image)"
        >
          {{ t('gallery.taxonomySuggest.button') }}
        </button>
      </div>
    </div>
  </article>
</template>
