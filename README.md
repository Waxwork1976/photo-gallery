# Photo Paccots

A personal nature and garden photography gallery built on Azure.

## Recent Updates

- **Gallery display modes:** Added dynamic gallery behavior with folder-aware rendering:
  - random slideshow + child folder cards at root and intermediate levels
  - full image grid at leaf folders
  - optional `Year/Month` navigation mode built from currently selected species context
- **Species metadata enrichment:** Added multilingual common-name enrichment (`en/fr/de/it`) and normalized taxonomy metadata for plants, birds, and insects.
  - Plant flow now returns PlantNet results immediately, then enriches common names asynchronously via Azure Queue + queue-trigger worker.
- **Search with suggestions:** Added search by common name and taxonomy with backend suggestions after 3 typed characters (gallery + manage).
- **Configurable slideshow settings:** Added admin settings page to control:
  - number of photos
  - slide interval
  - fade transition duration (with guard so transition cannot exceed interval)
  - visibility of the map section on the public `/project` page
- **Public project page:** Added `/project` page with localized long-form project description and optional OpenStreetMap markers built from photo coordinates.
- **Taxonomy suggestion management:** Added end-to-end suggestion submission + review workflow. Suggestions are persisted in Azure Table Storage, visible to authenticated managers, and can be accepted/rejected with optional author contact (`mailto:`). Accepted suggestions update image taxonomy metadata and append `Edited following expert request` to description.
- **Translation management upgrades:**
  - placeholder protection for `{...}` tokens during machine translation
  - per-translation lock (`autoTranslate`) to prevent overwriting manual edits
  - lock icon in translation UI (green unlocked, red locked)
- **Folder recomputation reliability:** Recompute flow now regenerates folder tree from tags, saves it, then recomputes assignments (including upload/delete and manual recompute action).
- **Common-name recomputation:** Added admin action/endpoint to recompute multilingual common names for existing images.
- **Public credits page:** Added `/powered-by` page, linked from footer as `Thanks to..`.

## Architecture

```
Browser ──► Azure Storage (Static Website)    ──► Nuxt 3 SSG (HTML/JS/CSS)
       ──► Azure Functions (.NET 8 Isolated)  ──► API + background workers
                  │
                  ├── Azure Blob Storage       ──► Photo files (private container)
                  ├── Azure Table Storage      ──► Photo metadata
                  ├── Azure Queue Storage      ──► Async species enrichment jobs
                  └── Microsoft Entra ID       ──► OAuth 2.0 authentication
```

**Front-end:** Nuxt 3 (static generation) with Tailwind CSS and MSAL for authentication.

**Back-end:** Azure Functions (C# / .NET 8, consumption plan) providing a REST API for listing images, generating SAS upload URLs, and saving metadata.

**Storage:** Images stored in a private Blob Storage container, served via time-limited SAS read tokens. Metadata (title, tags, description, species taxonomy, localized common names) stored in Table Storage.

**Auth:** Microsoft Entra ID (single tenant). Only authorized users can upload; the gallery is public.

## Global Functioning (End-to-End)

The platform runs as a public static gallery with authenticated management APIs:

1. A visitor opens the Nuxt static site hosted in Azure Storage Static Website.
2. Public pages call Azure Functions endpoints to fetch gallery metadata (`list-images`, `folder-tree`, `translations`, `slideshow-settings`).
3. The API reads metadata from Azure Table Storage and generates short-lived SAS read URLs for private photo blobs.
4. The browser displays images directly from Blob Storage using those SAS URLs.

For authenticated workflows:

1. A manager signs in with Microsoft Entra ID in the front-end (MSAL).
2. Protected API calls send a bearer token, validated by Azure Functions.
3. Upload flow requests a write-only SAS URL, uploads the file to Blob Storage, then saves metadata in Table Storage.
4. Species identification endpoints call PlantNet (plants), RapidAPI bird-classifier (birds), and Gemini (insects).
5. Background queue workers enrich multilingual common names and persist updates in Table Storage.
6. Manage pages (images, translations, settings, taxonomy suggestions) update state through secured API endpoints.

## Project Structure

```
PhotoPaccots/
├── photo-paccots/              # Nuxt 3 front-end
│   ├── app/
│   │   ├── components/         # Vue components (gallery, upload, layout)
│   │   ├── composables/        # useAuth, useApi, useUpload
│   │   ├── pages/              # index, project, upload, manage, manage-translations, manage-settings, powered-by, auth/callback
│   │   ├── plugins/            # MSAL client plugin
│   │   └── middleware/         # Auth route guard
│   ├── .env.example            # Environment template
│   └── nuxt.config.ts
│
├── photo-functions/            # Azure Functions API (.NET 8)
│   └── src/PhotoFunctions/
│       ├── Functions/          # HTTP endpoints
│       ├── Services/           # JWT validation, Blob & Table storage
│       ├── Models/             # Entities, request/response DTOs
│       └── Configuration/     # Options classes
│
├── ARCHITECTURE.md             # Detailed architecture documentation
└── InstallationScripts/        # Deployment scripts (gitignored, contains secrets)
```

## Requirements

- [Node.js](https://nodejs.org/) 18+
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- An Azure subscription
- A Microsoft Entra ID tenant with an App Registration

## Azure Resources

| Resource | Name | Purpose |
|----------|------|---------|
| Resource Group | `rg-photo-gallery` | Container for all resources |
| Storage Account | `waxphotogallerystore` | Static website, blob & table storage |
| Function App | `wax-photogallery-api` | API (consumption plan) |
| App Registration | `PhotoGallery` | OAuth authentication |

## Required Services

To run the full application (gallery + upload + plant/bird/insect identification + multilingual common-name enrichment), these services are required.

For a quick list of where to obtain each required key/ID/URL, see `KEYS-SETUP-README.md`.

### 1) Microsoft Azure

- **Azure Subscription**: hosts all cloud resources.
- **Resource Group**: container for all resources (example: `rg-photo-gallery`).
- **Storage Account (StorageV2)**:
  - Static website hosting for Nuxt output (`$web`)
  - Blob container for images (`photos`)
  - Blob container for folder structure data (`folder-structure`)
  - Table Storage for metadata (`photos`)
- **Function App (Linux/Windows, .NET 8 isolated, v4)**:
  - API endpoints for listing/upload/auth/identification
- **Microsoft Entra ID App Registration**:
  - SPA login for frontend
  - JWT validation for protected function endpoints

### 2) External APIs

- **PlantNet API** (required for plant identification)
  - Create/register a PlantNet API key.
  - Configure key in Function App setting: `PlantNet__ApiKey`.

- **RapidAPI Bird Classifier API** (required for bird identification)
  - Create a RapidAPI account.
  - Subscribe to the `bird-classifier.p.rapidapi.com` API plan (free/paid plan as needed).
  - Generate or copy your RapidAPI key from that subscribed account.
  - Configure in Function App settings:
    - `BirdApi__ApiKey`
    - `BirdApi__Host` (expected: `bird-classifier.p.rapidapi.com`)
    - `BirdApi__BaseUrl` (expected: `https://bird-classifier.p.rapidapi.com`)
    - `BirdApi__Path` (expected: `/BirdClassifier/prediction`)
    - `BirdApi__ResultsCount` (example: `5`)
    - `BirdApi__MinProbability` (example: `0.5`)

- **Google Gemini API** (required for insect identification)
  - Create a Google AI API key for Gemini.
  - Configure in Function App settings:
    - `Gemini__ApiKey`
    - `Gemini__Model` (example: `gemini-3-flash-preview`)
    - `Gemini__BaseUrl` (expected: `https://generativelanguage.googleapis.com`)
    - `Gemini__TimeoutSeconds` (example: `30`)
  - Also used to enrich multilingual common names (`en/fr/de/it`) for plants, birds, and insects.
  - Plant common names are enriched in the background after metadata save (non-blocking upload UX).

#### External API registration checklist

1. PlantNet key created and active.
2. RapidAPI account created.
3. RapidAPI subscription for bird classifier is active.
4. `PlantNet__ApiKey`, `BirdApi__ApiKey`, and `Gemini__ApiKey` are set in Function App settings.
5. Function App restarted after setting/rotating API keys.

### 3) Required Function App Settings

At minimum, set all of the following on Azure Function App:

- `AzureStorage__AccountName`
- `AzureStorage__AccountKey`
- `AzureStorage__PhotoContainerName`
- `AzureStorage__PhotoTableName`
- `AzureStorage__TaxonomySuggestionTableName` (optional, default: `taxonomysuggestions`)
- `AzureStorage__UsePrivateContainer`
- `AzureAd__TenantId`
- `AzureAd__ClientId`
- `AzureAd__AllowedUserOids`
- `PlantNet__ApiKey`
- `BirdApi__ApiKey`
- `BirdApi__Host`
- `BirdApi__BaseUrl`
- `BirdApi__Path`
- `BirdApi__ResultsCount`
- `BirdApi__MinProbability`
- `Gemini__ApiKey`
- `Gemini__Model`
- `Gemini__BaseUrl`
- `Gemini__TimeoutSeconds`
- `SpeciesEnrichment__QueueName`
- `Translator__Endpoint`
- `Translator__ApiKey`
- `Translator__Region`

### 4) Required Frontend Environment Variables

Set these in `photo-paccots/.env` (local) and `.env.production` (deployment):

- `NUXT_PUBLIC_AZURE_CLIENT_ID`
- `NUXT_PUBLIC_AZURE_TENANT_ID`
- `NUXT_PUBLIC_AZURE_REDIRECT_URI`
- `NUXT_PUBLIC_API_BASE_URL`
- `NUXT_PUBLIC_TURNSTILE_SITE_KEY` (required for guest taxonomy-suggestion CAPTCHA)

### 5) Local Tooling

- Node.js 18+
- npm
- .NET 8 SDK
- Azure CLI
- Azure Functions Core Tools v4

## Local Development

### Front-end

```bash
cd photo-paccots
npm install
# Configure .env (copy from .env.example and fill in values)
npm run dev
```

Runs at `http://localhost:3000`.

### Back-end

```bash
cd photo-functions/src/PhotoFunctions
dotnet restore
# Configure local.settings.json (see ARCHITECTURE.md section 8)
func start
```

Runs at `http://localhost:7071`.

## API Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/health` | GET | Public | Health check |
| `/api/list-images` | GET | Public | List gallery images (with SAS read URLs) |
| `/api/folder-tree` | GET | Public | Read folder tree and labels |
| `/api/translations` | GET | Public | Read public UI translations |
| `/api/slideshow-settings` | GET | Public | Read slideshow settings |
| `/api/auth-validate` | POST | Bearer JWT | Validate token and check authorization |
| `/api/generate-sas` | POST | Bearer JWT | Get a time-limited SAS URL for upload |
| `/api/save-metadata` | POST | Bearer JWT | Save image metadata to Table Storage |
| `/api/update-metadata/{id}` | PUT | Bearer JWT | Update metadata for one image |
| `/api/delete-image/{id}` | DELETE | Bearer JWT | Delete image and metadata |
| `/api/manage-images` | GET | Bearer JWT | List all images for admin |
| `/api/manage-folder-tree` | GET/PUT | Bearer JWT | Read/update folder tree config |
| `/api/recompute-folder-assignments` | POST | Bearer JWT | Regenerate folders and recompute assignments |
| `/api/manage-translations` | GET/PUT | Bearer JWT | Read/update translations (with lock flags) |
| `/api/manage-translations/translate-all` | POST | Bearer JWT | Auto-translate all eligible keys |
| `/api/manage-translations/translate-key` | POST | Bearer JWT | Auto-translate one eligible key |
| `/api/manage-slideshow-settings` | GET/PUT | Bearer JWT | Read/update slideshow settings |
| `/api/identify-plant` | POST | Bearer JWT | Identify plant using PlantNet |
| `/api/identify-bird` | POST | Bearer JWT | Identify bird using RapidAPI |
| `/api/identify-insect` | POST | Bearer JWT | Identify insect using Gemini (returns multilingual common names + scientific taxonomy) |
| `/api/search-suggestions` | GET | Public | Search suggestions (common names + taxonomy), active from 3 characters |
| `/api/taxonomy-suggestions` | POST | Public | Submit a taxonomy suggestion from the gallery |
| `/api/manage-search-suggestions` | GET | Bearer JWT | Admin search suggestions across all images |
| `/api/manage-taxonomy-suggestions` | GET | Bearer JWT | List pending taxonomy suggestions |
| `/api/manage-taxonomy-suggestions/count` | GET | Bearer JWT | Count pending taxonomy suggestions for header badge |
| `/api/manage-taxonomy-suggestions/{id}/accept` | POST | Bearer JWT | Accept suggestion, update photo metadata, then remove suggestion |
| `/api/manage-taxonomy-suggestions/{id}/reject` | POST | Bearer JWT | Reject and remove suggestion |
| `/api/recompute-common-names` | POST | Bearer JWT | Recompute multilingual common names for existing images |

## Deployment

Deployment scripts are in `InstallationScripts/` (gitignored). Run in order:

1. `0_AzureProviderRegistrations.sh` — Register required Azure resource providers (Storage, Web, Insights, CognitiveServices)
2. `1_StorageAccount.sh` — Create resource group, storage account, blob container, table
3. `2_AppRegistration.sh` — Create Entra ID app registration
4. `3_1_AzureCognitiveService.sh` — Create Azure AI Translator cognitive account and output endpoint/key
5. `3_AzureFunction.sh` — Create function app, set CORS and app settings (including Translator settings)
6. `4_Deploy_function.ps1` — Build/deploy Azure Functions and re-apply `SpeciesEnrichment__QueueName`
7. `5_Deploy_nuxt.ps1` — Generate static site and upload to Azure Storage

### Deployment Notes

- Run scripts from repo root: `cd PhotoPaccots`
- Make scripts executable once:
  - `chmod +x InstallationScripts/0_AzureProviderRegistrations.sh`
  - `chmod +x InstallationScripts/3_1_AzureCognitiveService.sh`
  - `chmod +x InstallationScripts/3_AzureFunction.sh`
- `0_AzureProviderRegistrations.sh` fixes `MissingSubscriptionRegistration` errors (including `Microsoft.CognitiveServices` for Translator).
- `3_1_AzureCognitiveService.sh` outputs:
  - `TRANSLATOR_ENDPOINT`
  - `TRANSLATOR_KEY`
  - `COG_ACCOUNT_NAME`
- `3_AzureFunction.sh` can auto-discover Translator values from `COG_ACCOUNT_NAME`, or you can pass explicit `TRANSLATOR_ENDPOINT` / `TRANSLATOR_KEY`.
- If Translator settings were missing/rotated after deployment, run `fix_add_translation_key.sh` to update only:
  - `Translator__Endpoint`
  - `Translator__ApiKey`
  - `Translator__Region`
- If Gemini settings were missing/rotated after deployment, run `fix_add_gemini_settings.sh` to update only:
  - `Gemini__ApiKey`
  - `Gemini__Model`
  - `Gemini__BaseUrl`
  - `Gemini__TimeoutSeconds`
- If species enrichment queue settings were missing/changed, run `fix_add_species_enrichment_settings.sh` to update only:
  - `SpeciesEnrichment__QueueName`
- If an existing environment needs a one-shot runtime hardening pass, run `fix_apply_runtime_hardening.sh` to:
  - enforce private photo container access
  - normalize Blob/Function CORS (`z1`, remove legacy `z13`)
  - enforce `SpeciesEnrichment__QueueName` and `AzureStorage__UsePrivateContainer=true`
  - optionally re-apply Translator/Gemini/Turnstile settings when provided
- `4_Deploy_function.ps1` now also applies `SpeciesEnrichment__QueueName` after publish as a post-deploy safety step.
- If Turnstile settings were missing/changed, run `fix_add_turnstile_settings.sh` to update only:
  - `Turnstile__SiteKey`
  - `Turnstile__SecretKey`
  - Note: `Turnstile__SiteKey` is public by design in the browser. Keep it out of git, inject at deploy time.
- `5_Deploy_nuxt.ps1` injects `NUXT_PUBLIC_TURNSTILE_SITE_KEY` from:
  - `TURNSTILE_SITE_KEY` env var (preferred), or
  - `Turnstile__SiteKey` in Function App settings (if `RG_NAME` and `FUNCTION_APP_NAME` are set in the shell)

### Example (bash)

```bash
export RG_NAME="rg-photo-gallery"
export LOCATION="global"
export COG_ACCOUNT_NAME="waxphotogallerytranslator"

./InstallationScripts/0_AzureProviderRegistrations.sh
./InstallationScripts/3_1_AzureCognitiveService.sh
```

### Fix Translator settings only

```bash
export RG_NAME="rg-photo-gallery"
export FUNCTION_APP_NAME="wax-photogallery-api"
export COG_ACCOUNT_NAME="waxphotogallerytranslator"

./InstallationScripts/fix_add_translation_key.sh
```

### Fix Gemini settings only

```bash
export RG_NAME="rg-photo-gallery"
export FUNCTION_APP_NAME="wax-photogallery-api"
export GEMINI_API_KEY="<your-gemini-api-key>"
# Optional:
# export GEMINI_MODEL="gemini-3-flash-preview"
# export GEMINI_BASE_URL="https://generativelanguage.googleapis.com"
# export GEMINI_TIMEOUT_SECONDS="30"

./InstallationScripts/fix_add_gemini_settings.sh
```

### Fix species enrichment queue settings only

```bash
export RG_NAME="rg-photo-gallery"
export FUNCTION_APP_NAME="wax-photogallery-api"
# Optional:
# export SPECIES_ENRICHMENT_QUEUE_NAME="species-enrichment-queue"

./InstallationScripts/fix_add_species_enrichment_settings.sh
```

## Security

- Only users listed in `ALLOWED_USER_OIDS` can upload images
- JWT tokens are validated against Microsoft's OIDC discovery endpoint
- SAS upload tokens are write-only with 10-minute expiry
- SAS read tokens are generated per-request with 1-hour expiry
- Blob container is private (no anonymous access)
- Storage account keys are never exposed to the client

## Cost

Estimated monthly cost for low traffic (~1,000 views, ~100 uploads): **under $1/month**.

- Azure Functions: consumption plan (1M free executions/month)
- Storage: Standard LRS (~$0.02/GB/month)
- Bandwidth: first 100 GB/month free
