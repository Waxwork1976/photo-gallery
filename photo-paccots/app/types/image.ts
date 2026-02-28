/** A single image returned by the list-images endpoint. */
export interface ImageDto {
  id: string
  blobName: string
  fullUrl: string
  thumbnailUrl: string
  title: string
  description: string
  tags: string[]
  primaryFolderPath: string
  folderPaths: string[]
  uploadedAt: string
  width: number
  height: number
  latitude?: number
  longitude?: number
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
  fullUrl: string
  thumbnailUrl: string
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
  latitude?: number
  longitude?: number
}

/** Request body for PUT /api/update-metadata/{id} */
export interface UpdateMetadataRequest {
  title: string
  description: string
  tags: string[]
}

/** Response from PUT /api/update-metadata/{id} */
export interface UpdateMetadataResponse {
  success: boolean
  id: string
}

/** Response from DELETE /api/delete-image/{id} */
export interface DeleteImageResponse {
  success: boolean
  id: string
}

export interface FolderTreeNodeDto {
  name: string
  path: string
  children: FolderTreeNodeDto[]
}

export interface FolderTreeResponse {
  root: string
  tagRules: Record<string, string>
  rootLabels: Record<string, string>
  tree: FolderTreeNodeDto[]
}

export interface ManageFolderTreeResponse {
  root: string
  folderPaths: string[]
  tagRules: Record<string, string>
  rootLabels: Record<string, string>
}

export interface ManageFolderTreeRequest {
  folderPaths: string[]
  tagRules: Record<string, string>
  rootLabels?: Record<string, string>
}

export interface RecomputeFolderAssignmentsResponse {
  success: boolean
  total: number
  updated: number
}

export interface SlideshowSettingsDto {
  photoCount: number
  intervalSeconds: number
  transitionSeconds: number
}

export interface ManageSlideshowSettingsRequest {
  photoCount: number
  intervalSeconds: number
  transitionSeconds: number
}
