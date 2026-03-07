export interface TaxonomySuggestionTaxonomy {
  order: string
  family: string
  genus: string
  scientificName: string
  commonName: string
}

export interface TaxonomySuggestionRequester {
  authenticated: boolean
  name: string
  email: string
  locale: string
}

export interface CreateTaxonomySuggestionRequest {
  imageId: string
  imageTitle: string
  sourceTaxonomy: TaxonomySuggestionTaxonomy
  suggestedTaxonomy: TaxonomySuggestionTaxonomy
  note: string
  requester: TaxonomySuggestionRequester
  captchaToken?: string
}

export interface CreateTaxonomySuggestionResponse {
  success: boolean
  id: string
  createdAt: string
}

export interface TaxonomySuggestion {
  id: string
  imageId: string
  imageTitle: string
  sourceTaxonomy: TaxonomySuggestionTaxonomy
  suggestedTaxonomy: TaxonomySuggestionTaxonomy
  note: string
  requester: TaxonomySuggestionRequester
  createdAt: string
}

export interface ListTaxonomySuggestionsResponse {
  suggestions: TaxonomySuggestion[]
}

export interface TaxonomySuggestionCountResponse {
  pendingCount: number
}
