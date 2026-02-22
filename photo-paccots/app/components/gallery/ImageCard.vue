<script setup lang="ts">
import type { ImageDto } from '~/types/image'

const props = defineProps<{
  image: ImageDto
}>()

const emit = defineEmits<{
  click: [image: ImageDto]
}>()

const loaded = ref(false)

/** Human-readable file size. */
const formattedSize = computed(() => {
  const bytes = props.image.sizeBytes
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
})
</script>

<template>
  <article
    class="group cursor-pointer overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-stone-200 transition-all duration-300 hover:shadow-lg hover:ring-emerald-300"
    @click="emit('click', image)"
  >
    <!-- Image wrapper with aspect-ratio -->
    <div class="relative aspect-[4/3] overflow-hidden bg-stone-100">
      <!-- Skeleton loader -->
      <div
        v-if="!loaded"
        class="absolute inset-0 animate-pulse bg-gradient-to-br from-stone-200 to-stone-100"
      />

      <img
        :src="image.url"
        :alt="image.title || 'Gallery photo'"
        :width="image.width || undefined"
        :height="image.height || undefined"
        loading="lazy"
        class="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
        :class="{ 'opacity-0': !loaded }"
        @load="loaded = true"
      >

      <!-- Featured badge -->
      <span
        v-if="image.featured"
        class="absolute left-2 top-2 rounded-full bg-amber-400 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-amber-900 shadow"
      >
        Featured
      </span>
    </div>

    <!-- Card body -->
    <div class="px-4 py-3">
      <h3 class="truncate text-sm font-semibold text-stone-800 group-hover:text-emerald-700 transition-colors">
        {{ image.title || 'Untitled' }}
      </h3>

      <p v-if="image.description" class="mt-0.5 line-clamp-2 text-xs text-stone-500">
        {{ image.description }}
      </p>

      <div class="mt-2 flex flex-wrap items-center gap-1.5">
        <span
          v-for="tag in image.tags.slice(0, 4)"
          :key="tag"
          class="rounded-full bg-stone-100 px-2 py-0.5 text-[10px] font-medium text-stone-500"
        >
          {{ tag }}
        </span>
        <span v-if="image.tags.length > 4" class="text-[10px] text-stone-400">
          +{{ image.tags.length - 4 }}
        </span>
      </div>

      <div class="mt-2 flex items-center justify-between text-[11px] text-stone-400">
        <time :datetime="image.uploadedAt">
          {{ new Date(image.uploadedAt).toLocaleDateString() }}
        </time>
        <span>{{ formattedSize }}</span>
      </div>
    </div>
  </article>
</template>
