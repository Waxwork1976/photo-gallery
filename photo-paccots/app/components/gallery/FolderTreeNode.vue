<script setup lang="ts">
import type { FolderTreeNodeDto } from '~/types/image'

defineOptions({
  name: 'GalleryFolderTreeNode',
})

const props = defineProps<{
  node: FolderTreeNodeDto
  selectedPath: string | null
}>()

const emit = defineEmits<{
  select: [path: string]
}>()

const isSelected = computed(() => props.selectedPath === props.node.path)
</script>

<template>
  <li>
    <button
      type="button"
      class="rounded px-2 py-0.5 text-left text-sm transition-colors"
      :class="isSelected ? 'bg-emerald-100 text-emerald-800' : 'text-stone-600 hover:bg-stone-100'"
      @click="emit('select', node.path)"
    >
      {{ node.name }}
    </button>
    <ul v-if="node.children?.length" class="ml-4 mt-1 space-y-1 border-l border-stone-200 pl-2">
      <GalleryFolderTreeNode
        v-for="child in node.children"
        :key="child.path"
        :node="child"
        :selected-path="selectedPath"
        @select="emit('select', $event)"
      />
    </ul>
  </li>
</template>
