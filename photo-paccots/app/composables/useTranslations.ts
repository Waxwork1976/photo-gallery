import type { SupportedLocale, TranslationDictionary, TranslationsResponse } from '~/types/translation'

const SUPPORTED_LOCALES: SupportedLocale[] = ['en', 'fr', 'de', 'it']
const LOCALE_STORAGE_KEY = 'photo-paccots-locale'

const defaultEn: TranslationDictionary = {
  'app.brand': 'Photo Paccots',
  'nav.gallery': 'Gallery',
  'nav.upload': 'Upload',
  'nav.manage': 'Manage',
  'footer.thanksTo': 'Thanks to..',
  'auth.signIn': 'Sign in',
  'auth.signOut': 'Sign out',

  'gallery.title': 'Nature & Garden Gallery',
  'gallery.subtitle': 'A curated collection of nature and garden photography',
  'gallery.retry': 'Retry',
  'gallery.loadError': 'Failed to load images',
  'gallery.search.placeholder': 'Search by common name or taxonomy',
  'gallery.search.clear': 'Clear',
  'gallery.search.suggestions': 'Suggestions',
  'gallery.search.minChars': 'Type at least {count} characters to get suggestions.',
  'gallery.search.loading': 'Loading suggestions...',
  'gallery.search.resultsCount': '{count} matching image(s)',
  'gallery.search.noResults': 'No matching images found.',
  'gallery.search.loadError': 'Failed to load suggestions.',
  'folders.title': 'Folders',
  'folders.clear': 'Clear',
  'folders.root.photos': 'Photos',
  'folders.root.plants': 'Plants',
  'folders.root.birds': 'Birds',
  'folders.mode.primary': 'Species',
  'folders.mode.yearMonth': 'Year/Month',

  'upload.page.title': 'Upload a Photo',
  'upload.page.subtitle': 'Select an image, add some details, and share it with the gallery.',
  'upload.identification.noPlant': 'No plant identified in this image.',
  'upload.identification.lowConfidencePlant': 'Plant result confidence ({score}%) is below threshold ({threshold}%). Choose one of the top matches.',
  'upload.identification.lowConfidenceInsect': 'Insect result confidence is low ({score}%). You can still edit the fields before upload.',
  'upload.identification.topMatches': 'Top matches',
  'upload.identification.useMatch': 'Use this match',
  'upload.identification.confidence': 'Confidence: {score}%',
  'upload.identification.noReferenceImage': 'No image',
  'upload.identification.wildlife': 'Wildlife',
  'upload.identification.wildlifeHelp': 'Uncheck to set the second tag to cultivated instead of wildlife.',
  'upload.identification.identifyBird': 'Identify bird',
  'upload.identification.identifyingBird': 'Identifying bird...',
  'upload.identification.identifyInsect': 'Identify insect',
  'upload.identification.identifyingInsect': 'Identifying insect...',
  'upload.identification.resultPlant': 'Plant identified!',
  'upload.identification.resultBird': 'Bird identified!',
  'upload.identification.resultInsect': 'Insect identified!',
  'upload.identification.resultFilled': 'Fields have been filled automatically.',

  'manage.title': 'Image Management',
  'manage.totalCount': '{count} image(s) total',
  'manage.refresh': 'Refresh',
  'manage.loading': 'Loading...',
  'manage.empty': 'No images yet.',
  'manage.failedLoad': 'Failed to load images.',
  'manage.folderStructure': 'Folder Structure',
  'manage.folderRootHint': 'Root must remain photos. Tag rules map tag to folder path.',
  'manage.recompute': 'Recompute tags and folders',
  'manage.recomputing': 'Recomputing tags and folders...',
  'manage.recomputeCommonNames': 'Recompute common names',
  'manage.recomputingCommonNames': 'Recomputing common names...',
  'manage.generateFolders': 'Generate folders from tags',
  'manage.saveFolders': 'Save folders',
  'manage.loadingFolders': 'Loading folder structure...',
  'manage.edit': 'Edit',
  'manage.delete': 'Delete',
  'manage.save': 'Save',
  'manage.saving': 'Saving...',
  'manage.cancel': 'Cancel',
  'manage.search.placeholder': 'Search by common name or taxonomy',
  'manage.search.clear': 'Clear',
  'manage.search.suggestions': 'Suggestions',
  'manage.search.minChars': 'Type at least {count} characters to get suggestions.',
  'manage.search.loading': 'Loading suggestions...',
  'manage.search.resultsCount': '{count} matching image(s)',
  'manage.search.noResults': 'No matching images found.',
  'manage.search.loadError': 'Failed to load suggestions.',

  'manage.submenu.images': 'Images',
  'manage.submenu.translations': 'Translations',
  'manage.submenu.settings': 'Settings',

  'settings.title': 'Gallery Settings',
  'settings.subtitle': 'Configure slideshow behavior used in the gallery.',
  'settings.photoCount': 'Number of photos in slideshow',
  'settings.intervalSeconds': 'Slide interval (seconds)',
  'settings.transitionSeconds': 'Fade transition (seconds)',
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
  'translations.updateAll': 'Translate all keys from the selected language',
  'translations.updateOne': 'Translate this key',
  'translations.updatingAll': 'Updating all...',
  'translations.updatingOne': 'Updating...',
  'translations.successUpdateAll': 'All translations updated in draft.',
  'translations.successUpdateOne': 'Translation updated in draft.',
  'translations.autoTranslateOn': 'Auto-translation enabled (click to lock)',
  'translations.autoTranslateOff': 'Auto-translation locked (click to unlock)',

  'poweredBy.title': 'Thanks to:',
  'poweredBy.subtitle': 'Technologies and APIs used by Photo Paccots.',
  'poweredBy.logoTitle': 'Website logo',
  'poweredBy.logoDescription': 'Photo Paccots visual brand mark.',
  'poweredBy.appApis': 'App API endpoints',
  'poweredBy.externalApis': 'External APIs and platforms',
  'poweredBy.publicEndpoint': 'Public endpoint',
  'poweredBy.authEndpoint': 'Auth required',
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
