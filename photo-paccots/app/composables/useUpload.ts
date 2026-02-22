/**
 * Composable that orchestrates the full image upload flow:
 *  1. Request a SAS URL from the API
 *  2. Upload the file directly to Azure Blob Storage
 *  3. Save metadata via the API
 */
export const useUpload = () => {
  const state = reactive({
    isUploading: false,
    progress: 0,
    error: null as string | null,
  })

  const api = useApi()

  const uploadImage = async (
    file: File,
    metadata: {
      title: string
      description: string
      tags: string[]
    },
  ) => {
    state.isUploading = true
    state.progress = 0
    state.error = null

    try {
      // Step 1 — obtain a time-limited SAS URL
      state.progress = 10
      const { sasUrl, blobName } = await api.generateSasUrl(
        file.name,
        file.type,
      )

      // Step 2 — upload the blob directly (bypasses Azure Functions)
      state.progress = 20
      await api.uploadToBlob(sasUrl, file, (progress) => {
        // Map upload progress to the 20 %–80 % range
        state.progress = 20 + progress * 0.6
      })

      // Step 3 — persist metadata in Table Storage
      state.progress = 85
      await api.saveMetadata({
        blobName,
        originalFilename: file.name,
        title: metadata.title,
        description: metadata.description,
        tags: metadata.tags,
        contentType: file.type,
        sizeBytes: file.size,
      })

      state.progress = 100
      state.isUploading = false

      return { success: true, blobName }
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : 'Upload failed'
      state.error = message
      state.isUploading = false
      throw err
    }
  }

  const reset = () => {
    state.isUploading = false
    state.progress = 0
    state.error = null
  }

  return {
    state: readonly(state),
    uploadImage,
    reset,
  }
}
