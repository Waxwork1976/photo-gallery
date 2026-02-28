import type { SupportedLocale, TranslationDictionary, TranslationsResponse } from '~/types/translation'

const SUPPORTED_LOCALES: SupportedLocale[] = ['en', 'fr', 'de', 'it']
const LOCALE_STORAGE_KEY = 'photo-paccots-locale'

const defaultEn: TranslationDictionary = {
  'app.brand': 'Photo Paccots',
  'nav.gallery': 'Gallery',
  'nav.upload': 'Upload',
  'nav.manage': 'Manage',
  'auth.signIn': 'Sign in',
  'auth.signOut': 'Sign out',

  'gallery.title': 'Nature & Garden Gallery',
  'gallery.subtitle': 'A curated collection of nature and garden photography',
  'gallery.retry': 'Retry',
  'gallery.loadError': 'Failed to load images',
  'folders.title': 'Folders',
  'folders.clear': 'Clear',
  'folders.root.photos': 'Photos',
  'folders.root.plants': 'Plants',
  'folders.root.birds': 'Birds',

  'upload.page.title': 'Upload a Photo',
  'upload.page.subtitle': 'Select an image, add some details, and share it with the gallery.',

  'manage.title': 'Image Management',
  'manage.totalCount': '{count} image(s) total',
  'manage.refresh': 'Refresh',
  'manage.loading': 'Loading...',
  'manage.empty': 'No images yet.',
  'manage.failedLoad': 'Failed to load images.',
  'manage.folderStructure': 'Folder Structure',
  'manage.folderRootHint': 'Root must remain photos. Tag rules map tag to folder path.',
  'manage.recompute': 'Recompute image folders',
  'manage.recomputing': 'Recomputing...',
  'manage.generateFolders': 'Generate folders from tags',
  'manage.saveFolders': 'Save folders',
  'manage.loadingFolders': 'Loading folder structure...',
  'manage.edit': 'Edit',
  'manage.delete': 'Delete',
  'manage.save': 'Save',
  'manage.saving': 'Saving...',
  'manage.cancel': 'Cancel',

  'manage.submenu.images': 'Images',
  'manage.submenu.translations': 'Translations',
  'manage.submenu.settings': 'Settings',

  'settings.title': 'Gallery Settings',
  'settings.subtitle': 'Configure slideshow behavior used in the gallery.',
  'settings.photoCount': 'Number of photos in slideshow',
  'settings.intervalSeconds': 'Slide interval (seconds)',
  'settings.loadError': 'Failed to load settings.',
  'settings.saveError': 'Failed to save settings.',
  'settings.saveSuccess': 'Settings saved.',
  'settings.save': 'Save settings',

  'translations.title': 'Translations',
  'translations.subtitle': 'Customize UI text for each supported language.',
  'translations.language': 'Language',
  'translations.loading': 'Loading translations...',
  'translations.errorLoad': 'Failed to load translations.',
  'translations.errorSave': 'Failed to save translations.',
  'translations.successSave': 'Translations saved.',
  'translations.search': 'Search keys',
  'translations.reset': 'Reset',
  'translations.save': 'Save translations',
  'translations.targetLanguage': 'Target language',
  'translations.allLanguages': 'All languages',
  'translations.updateAll': 'Update all from selected language',
  'translations.updateOne': 'Update this key',
  'translations.updatingAll': 'Updating all...',
  'translations.updatingOne': 'Updating...',
  'translations.successUpdateAll': 'All translations updated in draft.',
  'translations.successUpdateOne': 'Translation updated in draft.',
}

const defaultLocales: Record<SupportedLocale, TranslationDictionary> = {
  en: { ...defaultEn },
  fr: {},
  de: {},
  it: {},
}

const interpolate = (template: string, params?: Record<string, string | number>) => {
  if (!params) return template
  return template.replace(/\{(\w+)\}/g, (_, key: string) => String(params[key] ?? `{${key}}`))
}

export const useTranslations = () => {
  const api = useApi()

  const locale = useState<SupportedLocale>('ui-locale', () => 'en')
  const remoteLocales = useState<TranslationsResponse['locales']>('ui-remote-locales', () => ({
    en: {},
    fr: {},
    de: {},
    it: {},
  }))
  const loaded = useState<boolean>('ui-translations-loaded', () => false)

  const locales = computed(() => {
    return {
      en: { ...defaultLocales.en, ...(remoteLocales.value.en ?? {}) },
      fr: { ...defaultLocales.fr, ...(remoteLocales.value.fr ?? {}) },
      de: { ...defaultLocales.de, ...(remoteLocales.value.de ?? {}) },
      it: { ...defaultLocales.it, ...(remoteLocales.value.it ?? {}) },
    } satisfies Record<SupportedLocale, TranslationDictionary>
  })

  const t = (key: string, params?: Record<string, string | number>) => {
    const activeLocale = locale.value
    const value = locales.value[activeLocale]?.[key]
      ?? locales.value.en[key]
      ?? key
    return interpolate(value, params)
  }

  const setLocale = (next: SupportedLocale) => {
    locale.value = next
    if (import.meta.client) {
      localStorage.setItem(LOCALE_STORAGE_KEY, next)
    }
  }

  const initLocale = () => {
    if (!import.meta.client) return
    const stored = localStorage.getItem(LOCALE_STORAGE_KEY)
    if (stored && SUPPORTED_LOCALES.includes(stored as SupportedLocale)) {
      locale.value = stored as SupportedLocale
    }
  }

  const loadTranslations = async () => {
    try {
      const res = await api.getTranslations()
      remoteLocales.value = {
        en: res.locales?.en ?? {},
        fr: res.locales?.fr ?? {},
        de: res.locales?.de ?? {},
        it: res.locales?.it ?? {},
      }
    } finally {
      loaded.value = true
    }
  }

  const setRemoteLocales = (localesPayload: TranslationsResponse['locales']) => {
    remoteLocales.value = {
      en: localesPayload.en ?? {},
      fr: localesPayload.fr ?? {},
      de: localesPayload.de ?? {},
      it: localesPayload.it ?? {},
    }
  }

  return {
    locale,
    locales,
    supportedLocales: SUPPORTED_LOCALES,
    defaultLocales,
    loaded,
    t,
    setLocale,
    initLocale,
    loadTranslations,
    setRemoteLocales,
  }
}
