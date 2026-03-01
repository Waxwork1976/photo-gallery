export interface InsectIdentificationResponse {
  accepted: boolean
  confidence: number
  commonName: string
  scientificName: string
  taxonomyOrder: string
  taxonomyFamily: string
  taxonomyGenus: string
}
