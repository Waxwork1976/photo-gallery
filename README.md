# Photo Paccots

A personal nature and garden photography gallery built on Azure.

## Architecture

```
Browser ──► Azure Storage (Static Website)    ──► Nuxt 3 SSG (HTML/JS/CSS)
       ──► Azure Functions (.NET 8 Isolated)  ──► API (list, upload, auth)
                  │
                  ├── Azure Blob Storage       ──► Photo files (private container)
                  ├── Azure Table Storage      ──► Photo metadata
                  └── Microsoft Entra ID       ──► OAuth 2.0 authentication
```

**Front-end:** Nuxt 3 (static generation) with Tailwind CSS and MSAL for authentication.

**Back-end:** Azure Functions (C# / .NET 8, consumption plan) providing a REST API for listing images, generating SAS upload URLs, and saving metadata.

**Storage:** Images stored in a private Blob Storage container, served via time-limited SAS read tokens. Metadata (title, tags, description) stored in Table Storage.

**Auth:** Microsoft Entra ID (single tenant). Only authorized users can upload; the gallery is public.

## Project Structure

```
PhotoPaccots/
├── photo-paccots/              # Nuxt 3 front-end
│   ├── app/
│   │   ├── components/         # Vue components (gallery, upload, layout)
│   │   ├── composables/        # useAuth, useApi, useUpload
│   │   ├── pages/              # index (gallery), upload, auth/callback
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

To run the full application (gallery + upload + plant/bird identification), these services are required.

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

#### External API registration checklist

1. PlantNet key created and active.
2. RapidAPI account created.
3. RapidAPI subscription for bird classifier is active.
4. `PlantNet__ApiKey` and `BirdApi__ApiKey` are set in Function App settings.
5. Function App restarted after setting/rotating API keys.

### 3) Required Function App Settings

At minimum, set all of the following on Azure Function App:

- `AzureStorage__AccountName`
- `AzureStorage__AccountKey`
- `AzureStorage__PhotoContainerName`
- `AzureStorage__PhotoTableName`
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
- `Translator__Endpoint`
- `Translator__ApiKey`
- `Translator__Region`

### 4) Required Frontend Environment Variables

Set these in `photo-paccots/.env` (local) and `.env.production` (deployment):

- `NUXT_PUBLIC_AZURE_CLIENT_ID`
- `NUXT_PUBLIC_AZURE_TENANT_ID`
- `NUXT_PUBLIC_AZURE_REDIRECT_URI`
- `NUXT_PUBLIC_API_BASE_URL`

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
| `/api/auth-validate` | POST | Bearer JWT | Validate token and check authorization |
| `/api/generate-sas` | POST | Bearer JWT | Get a time-limited SAS URL for upload |
| `/api/save-metadata` | POST | Bearer JWT | Save image metadata to Table Storage |

## Deployment

Deployment scripts are in `InstallationScripts/` (gitignored). Run in order:

1. `0_AzureProviderRegistrations.sh` — Register required Azure resource providers (Storage, Web, Insights, CognitiveServices)
2. `1_StorageAccount.sh` — Create resource group, storage account, blob container, table
3. `2_AppRegistration.sh` — Create Entra ID app registration
4. `3_1_AzureCognitiveService.sh` — Create Azure AI Translator cognitive account and output endpoint/key
5. `3_AzureFunction.sh` — Create function app, set CORS and app settings (including Translator settings)
6. `4_Deploy_function.ps1` — Build and deploy the Azure Functions
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
