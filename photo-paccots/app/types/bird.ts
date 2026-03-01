export interface BirdPredictionItem {
  scientificName: string
  probability: number
  commonNames?: Record<string, string>
  taxonomyOrder?: string
  taxonomyFamily?: string
  taxonomyGenus?: string
}

export interface BirdIdentificationResponse {
  minProbability: number
  accepted: boolean
  topResult: BirdPredictionItem | null
  results: BirdPredictionItem[]
}
