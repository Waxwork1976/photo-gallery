export type SupportedLocale = 'en' | 'fr' | 'de' | 'it'

export type TranslationDictionary = Record<string, string>
export interface TranslationEntry {
  value: string
  autoTranslate: boolean
}
export type TranslationEntryDictionary = Record<string, TranslationEntry>

export interface TranslationsResponse {
  locales: Record<SupportedLocale, TranslationDictionary>
}

export interface ManageTranslationsRequest {
  locales: Record<SupportedLocale, TranslationEntryDictionary>
}

export interface ManageTranslationsResponse {
  locales: Record<SupportedLocale, TranslationEntryDictionary>
}

export interface TranslateAllTranslationsRequest {
  sourceLocale: SupportedLocale
  targetLocales?: SupportedLocale[]
  locales: Record<SupportedLocale, TranslationEntryDictionary>
}

export interface TranslateSingleTranslationRequest {
  sourceLocale: SupportedLocale
  key: string
  targetLocales?: SupportedLocale[]
  locales: Record<SupportedLocale, TranslationEntryDictionary>
}
