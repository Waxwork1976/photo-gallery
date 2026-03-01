export interface PlantNetSpecies {
  scientificNameWithoutAuthor: string
  scientificNameAuthorship: string
  scientificName: string
  genus: {
    scientificNameWithoutAuthor: string
    scientificName: string
  }
  family: {
    scientificNameWithoutAuthor: string
    scientificName: string
  }
  commonNames: string[]
}

export interface PlantNetResult {
  score: number
  species: PlantNetSpecies
  gbif?: { id: string }
  powo?: { id: string }
  images?: Array<{
    organ?: string
    citation?: string
    image?: string
    url?: string | {
      o?: string
      m?: string
      s?: string
    }
  }>
  similarImages?: Array<{
    image?: string
    url?: string | {
      o?: string
      m?: string
      s?: string
    }
  }>
}

export interface PlantNetResponse {
  query: Record<string, unknown>
  predictedOrgans: Array<{
    image: string
    filename: string
    organ: string
    score: number
  }>
  language: string
  bestMatch: string
  results: PlantNetResult[]
  otherResults?: unknown[]
  version: string
  remainingIdentificationRequests: number
}
