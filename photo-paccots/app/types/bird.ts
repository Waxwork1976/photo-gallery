export interface BirdPredictionItem {
  scientificName: string
  probability: number
}

export interface BirdIdentificationResponse {
  minProbability: number
  accepted: boolean
  topResult: BirdPredictionItem | null
  results: BirdPredictionItem[]
}
