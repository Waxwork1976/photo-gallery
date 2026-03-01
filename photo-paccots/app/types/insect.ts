export interface InsectIdentificationResponse {
  accepted: boolean
  confidence: number
  commonNames: Record<string, string>
  scientificName: string
  taxonomyOrder: string
  taxonomyFamily: string
  taxonomyGenus: string
}
