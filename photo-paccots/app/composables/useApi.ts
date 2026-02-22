import type {
  GenerateSasResponse,
  ListImagesResponse,
  SaveMetadataRequest,
  SaveMetadataResponse,
} from '~/types/image'
import type { PlantNetResponse } from '~/types/plantnet'

interface ApiError {
  message: string
  statusCode: number
}

/**
 * Composable that wraps all Azure Functions API calls.
 * Automatically attaches the Bearer JWT to protected endpoints.
 */
export const useApi = () => {
  const config = useRuntimeConfig()
  const { getAccessToken } = useAuth()

  const baseUrl = config.public.apiBaseUrl

  /** Generic authenticated fetch helper. */
  const fetchWithAuth = async <T>(
    endpoint: string,
    options: RequestInit = {},
  ): Promise<T> => {
    const token = await getAccessToken()

    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    }

    if (token) {
      ;(headers as Record<string, string>)['Authorization'] = `Bearer ${token}`
    }

    const response = await fetch(`${baseUrl}${endpoint}`, {
      ...options,
      headers,
    })

    if (!response.ok) {
      const error: ApiError = {
        message: `API error: ${response.statusText}`,
        statusCode: response.status,
      }
      throw error
    }

    return response.json() as Promise<T>
  }

  // ---- Public endpoints ----

  /** Fetch the list of public images (optionally featured only). */
  const listImages = async (featured?: boolean): Promise<ListImagesResponse> => {
    const query = featured ? '?featured=true' : ''
    return fetchWithAuth<ListImagesResponse>(`/api/list-images${query}`)
  }

  // ---- Protected endpoints ----

  /** Request a SAS upload URL for a given file. */
  const generateSasUrl = async (
    filename: string,
    contentType: string,
  ): Promise<GenerateSasResponse> => {
    return fetchWithAuth<GenerateSasResponse>('/api/generate-sas', {
      method: 'POST',
      body: JSON.stringify({ filename, contentType }),
    })
  }

  /** Persist image metadata in Table Storage after a successful upload. */
  const saveMetadata = async (
    metadata: SaveMetadataRequest,
  ): Promise<SaveMetadataResponse> => {
    return fetchWithAuth<SaveMetadataResponse>('/api/save-metadata', {
      method: 'POST',
      body: JSON.stringify(metadata),
    })
  }

  /**
   * Upload a file directly to Azure Blob Storage using a SAS URL.
   * Uses XMLHttpRequest so we can track upload progress.
   */
  const uploadToBlob = async (
    sasUrl: string,
    file: File,
    onProgress?: (progress: number) => void,
  ): Promise<void> => {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest()

      xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable && onProgress) {
          const progress = (e.loaded / e.total) * 100
          onProgress(progress)
        }
      })

      xhr.addEventListener('load', () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve()
        } else {
          reject(new Error(`Upload failed: ${xhr.statusText}`))
        }
      })

      xhr.addEventListener('error', () => {
        reject(new Error('Upload failed — network error'))
      })

      xhr.open('PUT', sasUrl)
      xhr.setRequestHeader('x-ms-blob-type', 'BlockBlob')
      xhr.setRequestHeader('Content-Type', file.type)
      xhr.send(file)
    })
  }

  /** Identify a plant from one or more images via the PlantNet pass-through API. */
  const identifyPlant = async (
    images: File[],
    organs?: string[],
  ): Promise<PlantNetResponse> => {
    const token = await getAccessToken()

    const formData = new FormData()
    for (const img of images) {
      formData.append('images', img)
    }
    if (organs) {
      for (const organ of organs) {
        formData.append('organs', organ)
      }
    }

    const headers: Record<string, string> = {}
    if (token) {
      headers['Authorization'] = `Bearer ${token}`
    }

    const response = await fetch(`${baseUrl}/api/identify-plant`, {
      method: 'POST',
      headers,
      body: formData,
    })

    if (!response.ok) {
      const error: ApiError = {
        message: `API error: ${response.statusText}`,
        statusCode: response.status,
      }
      throw error
    }

    return response.json() as Promise<PlantNetResponse>
  }

  return {
    listImages,
    generateSasUrl,
    saveMetadata,
    uploadToBlob,
    identifyPlant,
  }
}
