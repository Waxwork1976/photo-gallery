<script setup lang="ts">
import type { ImageDto } from '~/types/image'

defineProps<{
  images: ImageDto[]
  loading?: boolean
}>()

const emit = defineEmits<{
  select: [image: ImageDto]
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
      <svg xmlns="http://www.w3.org/2000/svg" class="mb-4 h-16 w-16 text-stone-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
        <path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
      </svg>
      <h3 class="text-lg font-medium text-stone-600">No photos yet</h3>
      <p class="mt-1 text-sm text-stone-400">Upload your first photo to get started.</p>
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
        @click="emit('select', image)"
      />
    </div>
  </section>
</template>
