export type SupportedLocale = 'en' | 'fr' | 'de' | 'it'

export type TranslationDictionary = Record<string, string>

export interface TranslationsResponse {
  locales: Record<SupportedLocale, TranslationDictionary>
}

export interface ManageTranslationsRequest {
  locales: Record<SupportedLocale, TranslationDictionary>
}

export interface TranslateAllTranslationsRequest {
  sourceLocale: SupportedLocale
  targetLocales?: SupportedLocale[]
  locales: Record<SupportedLocale, TranslationDictionary>
}

export interface TranslateSingleTranslationRequest {
  sourceLocale: SupportedLocale
  key: string
  targetLocales?: SupportedLocale[]
  locales: Record<SupportedLocale, TranslationDictionary>
}
