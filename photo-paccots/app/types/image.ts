/** A single image returned by the list-images endpoint. */
export interface ImageDto {
  id: string
  blobName: string
  url: string
  title: string
  description: string
  tags: string[]
  uploadedAt: string
  width: number
  height: number
  sizeBytes: number
  featured: boolean
}

/** Response from GET /api/list-images */
export interface ListImagesResponse {
  images: ImageDto[]
}

/** Response from POST /api/generate-sas */
export interface GenerateSasResponse {
  sasUrl: string
  blobName: string
  expiresInSeconds: number
}

/** Response from POST /api/save-metadata */
export interface SaveMetadataResponse {
  success: boolean
  id: string
  blobUrl: string
}

/** Request body for POST /api/save-metadata */
export interface SaveMetadataRequest {
  blobName: string
  originalFilename: string
  title: string
  description: string
  tags: string[]
  contentType: string
  sizeBytes: number
  width?: number
  height?: number
}
