<script setup lang="ts">
definePageMeta({
  middleware: 'auth',
})

useHead({
  title: 'Upload — Photo Paccots',
})

const router = useRouter()

const onUploaded = () => {
  // Navigate back to gallery after a successful upload
  router.push('/')
}
</script>

<template>
  <div class="mx-auto max-w-2xl">
    <section class="mb-8 text-center">
      <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
        Upload a Photo
      </h1>
      <p class="mt-2 text-sm text-stone-500">
        Select an image, add some details, and share it with the gallery.
      </p>
    </section>

    <UploadForm @uploaded="onUploaded" />
  </div>
</template>
