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

1. `1_StorageAccount.sh` — Create resource group, storage account, blob container, table
2. `2_AppRegistration.sh` — Create Entra ID app registration
3. `3_AzureFunction.sh` — Create function app, set CORS and app settings
4. `4_Deploy_function.ps1` — Build and deploy the Azure Functions
5. `5_Deploy_nuxt.ps1` — Generate static site and upload to Azure Storage

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
