<script setup lang="ts">
import { PhotoIcon } from '@heroicons/vue/24/outline'
import type { ImageDto } from '~/types/image'

defineProps<{
  images: ImageDto[]
  loading?: boolean
  showSuggestButton?: boolean
}>()

const emit = defineEmits<{
  select: [image: ImageDto]
  suggestTaxonomy: [image: ImageDto]
}>()
</script>

<template>
  <section>
    <!-- Skeleton grid while loading -->
    <div
      v-if="loading"
      class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3"
    >
      <div
        v-for="n in 6"
        :key="n"
        class="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-stone-200"
      >
        <div class="aspect-[4/3] animate-pulse bg-stone-200" />
        <div class="space-y-2 px-4 py-3">
          <div class="h-4 w-3/4 animate-pulse rounded bg-stone-200" />
          <div class="h-3 w-1/2 animate-pulse rounded bg-stone-100" />
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="images.length === 0"
      class="flex flex-col items-center justify-center py-24 text-center"
    >
      <PhotoIcon class="mb-4 h-14 w-14 text-stone-300" />
      <h3 class="text-lg font-medium text-stone-600">No photos yet</h3>
      <p class="mt-1 text-sm text-stone-500">Upload your first photo to get started.</p>
    </div>

    <!-- Image grid -->
    <div
      v-else
      class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3"
    >
      <GalleryImageCard
        v-for="image in images"
        :key="image.id"
        :image="image"
        :show-suggest-button="showSuggestButton"
        @click="emit('select', image)"
        @suggest-taxonomy="emit('suggestTaxonomy', image)"
      />
    </div>
  </section>
</template>
