<script setup lang="ts">
import type { FolderTreeNodeDto } from '~/types/image'

defineProps<{
  tree: FolderTreeNodeDto[]
  selectedPath: string | null
}>()

const emit = defineEmits<{
  select: [path: string]
  clear: []
}>()
</script>

<template>
  <aside class="rounded-xl border border-stone-200 bg-white p-4 shadow-sm">
    <div class="mb-2 flex items-center justify-between">
      <h2 class="text-sm font-semibold text-stone-900">Folders</h2>
      <button
        type="button"
        class="text-xs text-stone-500 underline hover:text-stone-700"
        @click="emit('clear')"
      >
        Clear
      </button>
    </div>
    <ul class="space-y-1">
      <GalleryFolderTreeNode
        v-for="node in tree"
        :key="node.path"
        :node="node"
        :selected-path="selectedPath"
        @select="emit('select', $event)"
      />
    </ul>
  </aside>
</template>
