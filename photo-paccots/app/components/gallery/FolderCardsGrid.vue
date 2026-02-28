<script setup lang="ts">
interface FolderCardItem {
  path: string
  label: string
  thumbnailUrl: string | null
}

defineProps<{
  title?: string
  cards: FolderCardItem[]
}>()

const emit = defineEmits<{
  select: [path: string]
}>()
</script>

<template>
  <section>
    <h2 v-if="title" class="mb-3 text-sm font-semibold text-stone-700">
      {{ title }}
    </h2>
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <button
        v-for="card in cards"
        :key="card.path"
        type="button"
        class="ui-focus-ring ui-transition-soft overflow-hidden rounded-xl border border-stone-200 bg-white text-left shadow-sm hover:shadow-md"
        @click="emit('select', card.path)"
      >
        <div class="aspect-[4/3] w-full bg-stone-100">
          <img
            v-if="card.thumbnailUrl"
            :src="card.thumbnailUrl"
            :alt="card.label"
            class="h-full w-full object-cover"
          >
          <div v-else class="flex h-full items-center justify-center text-xs text-stone-500">
            No preview
          </div>
        </div>
        <div class="px-3 py-2">
          <p class="line-clamp-1 text-sm font-medium text-stone-800">
            {{ card.label }}
          </p>
        </div>
      </button>
    </div>
  </section>
</template>
