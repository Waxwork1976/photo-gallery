# Photo Gallery Architecture Guide

## 1. High-Level Architecture

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Public Internet                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              Azure CDN (Optional, for performance)               │
└─────────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┴────────────────────┐
         ▼                                         ▼
┌──────────────────────┐                 ┌──────────────────────┐
│  Azure Storage       │                 │  Azure Functions     │
│  Static Website      │                 │  (Consumption Plan)  │
│  (Nuxt SSG)          │                 │                      │
│  - index.html        │◄────────────────┤  - auth-validate     │
│  - _nuxt/*           │  API calls      │  - generate-sas      │
│  - images/*          │                 │  - list-images       │
└──────────────────────┘                 │  - save-metadata     │
                                         └──────────────────────┘
                                                  │
                                                  │
                    ┌─────────────────────────────┼─────────────────────┐
                    ▼                             ▼                     ▼
         ┌────────────────────┐      ┌─────────────────────┐  ┌──────────────────┐
         │ Microsoft Entra ID │      │  Azure Blob Storage │  │ Azure Table      │
         │ (OAuth 2.0)        │      │  (Images)           │  │ Storage          │
         │                    │      │  - Container:       │  │ (Metadata)       │
         │ - App Registration │      │    photos (private) │  │                  │
         │ - Single Tenant    │      │                     │  │ Table: photos    │
         └────────────────────┘      └─────────────────────┘  └──────────────────┘
```

### Component Responsibilities

**Nuxt 3 SSG (Static Site)**
- Serves public gallery pages (pre-rendered at build time)
- Protected upload page (client-side auth check + server validation)
- Handles OAuth flow with Microsoft Entra ID
- Makes API calls to Azure Functions for data/SAS tokens

**Azure Functions (API Backend)**
- Validates JWT tokens from Microsoft Entra ID
- Generates SAS tokens for blob uploads
- Lists images with metadata from Table Storage
- Inserts/updates metadata after successful uploads
- Enforces access control (only your account can upload)

**Azure Blob Storage**
- Stores actual image files
- **Recommended: Private container + SAS read tokens** (better security, audit trail)
- Alternative: Public container (simpler, but no access control)

**Azure Table Storage**
- Stores image metadata (title, description, upload date, tags, etc.)
- PartitionKey strategy: `gallery` or year-based (`2026`, `2025`)
- RowKey: unique image ID (GUID or filename hash)

**Microsoft Entra ID**
- OAuth 2.0 authentication provider
- Single-tenant app registration (only your Azure AD tenant)
- ID tokens contain user claims for validation

---

## 2. Folder Structure

### Nuxt 3 Application

```
photo-gallery/
├── .env                          # Local environment variables
├── .env.production               # Production environment variables
├── nuxt.config.ts                # Nuxt configuration
├── package.json
├── tsconfig.json
│
├── app.vue                       # Root component
├── app.config.ts                 # App-level config
│
├── public/                       # Static assets
│   └── favicon.ico
│
├── assets/                       # Build-time assets
│   ├── css/
│   │   └── main.css
│   └── images/
│
├── components/                   # Vue components
│   ├── gallery/
│   │   ├── ImageCard.vue
│   │   ├── ImageGrid.vue
│   │   └── ImageLightbox.vue
│   ├── upload/
│   │   ├── UploadForm.vue
│   │   └── UploadProgress.vue
│   └── layout/
│       ├── Header.vue
│       └── Footer.vue
│
├── composables/                  # Composables
│   ├── useAuth.ts               # Authentication logic
│   ├── useApi.ts                # API client
│   └── useUpload.ts             # Upload logic
│
├── pages/                        # File-based routing
│   ├── index.vue                # Gallery home (public)
│   ├── upload.vue               # Upload page (protected)
│   └── auth/
│       └── callback.vue         # OAuth callback handler
│
├── middleware/                   # Route middleware
│   └── auth.ts                  # Authentication guard
│
├── layouts/                      # Layouts
│   ├── default.vue
│   └── admin.vue
│
├── server/                       # Server routes (used during SSG)
│   └── api/
│       └── config.ts            # Exposes runtime config to client
│
└── types/                        # TypeScript types
    ├── image.ts
    └── auth.ts
```

### Azure Functions Project (C# / .NET 8 Isolated Worker)

```
photo-functions/
├── PhotoFunctions.sln
│
└── src/
    └── PhotoFunctions/
        ├── PhotoFunctions.csproj         # Project file & NuGet references
        ├── Program.cs                     # DI composition root
        ├── host.json                      # Function host configuration
        ├── local.settings.json            # Local secrets (gitignored)
        ├── .gitignore
        │
        ├── Configuration/
        │   ├── AzureAdOptions.cs          # Entra ID settings
        │   └── AzureStorageOptions.cs     # Storage account settings
        │
        ├── Models/
        │   ├── PhotoEntity.cs             # Table Storage entity
        │   ├── Requests.cs                # API request DTOs
        │   └── Responses.cs               # API response DTOs
        │
        ├── Services/
        │   ├── IJwtValidationService.cs   # JWT validation contract
        │   ├── JwtValidationService.cs    # Entra ID JWT validation
        │   ├── IBlobStorageService.cs     # Blob storage contract
        │   ├── BlobStorageService.cs      # SAS URL generation
        │   ├── IPhotoTableService.cs      # Table storage contract
        │   └── PhotoTableService.cs       # Table CRUD operations
        │
        └── Functions/
            ├── AuthValidateFunction.cs    # POST /api/auth-validate
            ├── GenerateSasFunction.cs     # POST /api/generate-sas
            ├── SaveMetadataFunction.cs    # POST /api/save-metadata
            ├── ListImagesFunction.cs      # GET  /api/list-images
            └── HealthFunction.cs          # GET  /api/health
```

---

## 3. Authentication Flow

### Microsoft Entra ID Setup

1. **Create App Registration**
   - Name: `PhotoGallery-App`
   - Supported account types: Single tenant (your organization only)
   - Redirect URI: `https://yourdomain.z13.web.core.windows.net/auth/callback`
   - Platform: Single-page application (SPA)

2. **Configure Authentication**
   - Enable ID tokens
   - Add logout URL: `https://yourdomain.z13.web.core.windows.net`
   - No client secret needed (PKCE flow for SPA)

3. **API Permissions**
   - Microsoft Graph: `User.Read` (optional, for user profile)
   - OpenID permissions: `openid`, `profile`, `email`

### Authentication Flow Diagram

```
┌──────────┐                                                   ┌────────────────┐
│  User    │                                                   │ Microsoft      │
│ (Browser)│                                                   │ Entra ID       │
└────┬─────┘                                                   └───────┬────────┘
     │                                                                 │
     │  1. Navigate to /upload                                         │
     ├──────────────────────────────────────────────────►             │
     │                     Nuxt App                                    │
     │                                                                 │
     │  2. Check localStorage for valid token                          │
     │  (if no token or expired) ──────────────►                       │
     │                                                                 │
     │  3. Redirect to Microsoft login                                 │
     ├─────────────────────────────────────────────────────────────────►
     │        with PKCE challenge                                      │
     │                                                                 │
     │  4. User authenticates                                          │
     │◄────────────────────────────────────────────────────────────────┤
     │                                                                 │
     │  5. Redirect to /auth/callback?code=xxx                         │
     ├─────────────────────────────────────────────────────────────────►
     │                                                                 │
     │  6. Exchange code for ID token                                  │
     ├─────────────────────────────────────────────────────────────────►
     │                                                                 │
     │  7. Return ID token (JWT)                                       │
     │◄────────────────────────────────────────────────────────────────┤
     │                                                                 │
     │  8. Store token in localStorage                                 │
     │     Extract claims (name, email, oid)                           │
     │                                                                 │
     │  9. Redirect to /upload                                         │
     │                                                                 │
     │  10. Make API call to Azure Functions                           │
     │      (with Authorization: Bearer <token>)                       │
     ├──────────────────────────────────────────────────►             │
     │                 Azure Functions                                 │
     │                                                                 │
     │  11. Validate JWT signature & claims ───────────────────────────►
     │      Check issuer, audience, expiry      (Verify with JWKS)    │
     │      Check user OID matches allowed list                        │
     │                                          ◄─────────────────────│
     │                                                                 │
     │  12. Return protected resource                                  │
     │◄────────────────────────────────────────────────                │
```

### Token Validation Logic

Azure Functions validate the JWT by:
1. Fetching Microsoft's public keys (JWKS endpoint)
2. Verifying token signature
3. Validating claims:
   - `iss`: issuer matches your Entra ID tenant
   - `aud`: audience matches your client ID
   - `exp`: token not expired
   - `oid`: object ID matches your allowed user(s)

---

## 4. Image Upload Flow (with SAS Tokens)

### Flow Diagram

```
┌──────────┐                  ┌─────────────┐                ┌──────────────┐
│  User    │                  │ Azure       │                │ Azure Blob   │
│ (Upload  │                  │ Functions   │                │ Storage      │
│  Page)   │                  │             │                │              │
└────┬─────┘                  └──────┬──────┘                └──────┬───────┘
     │                               │                              │
     │  1. Select image file         │                              │
     │     (with metadata)           │                              │
     │                               │                              │
     │  2. POST /api/generate-sas    │                              │
     │     Authorization: Bearer JWT │                              │
     │     Body: { filename, type }  │                              │
     ├───────────────────────────────►                              │
     │                               │                              │
     │                               │  3. Validate JWT             │
     │                               │     Check user is authorized │
     │                               │                              │
     │                               │  4. Generate unique blob name│
     │                               │     (e.g., uuid + extension) │
     │                               │                              │
     │                               │  5. Create SAS token ────────►
     │                               │     (write permission, 5min) │
     │                               │◄─────────────────────────────┤
     │                               │                              │
     │  6. Return SAS URL            │                              │
     │◄───────────────────────────────┤                              │
     │   { sasUrl, blobName }        │                              │
     │                               │                              │
     │  7. PUT image directly to     │                              │
     │     SAS URL (client-side) ────────────────────────────────────►
     │     (bypasses Azure Functions)│                              │
     │                               │                              │
     │  8. Upload complete ◄─────────────────────────────────────────┤
     │                               │                              │
     │  9. POST /api/save-metadata   │                              │
     │     Authorization: Bearer JWT │                              │
     │     Body: { blobName,         │                              │
     │             title, desc... }  │                              │
     ├───────────────────────────────►                              │
     │                               │                              │
     │                               │  10. Validate JWT            │
     │                               │                              │
     │                               │  11. Insert to Table Storage │
     │                               │                              │
     │  12. Success response         │                              │
     │◄───────────────────────────────┤                              │
     │                               │                              │
     │  13. Redirect to gallery      │                              │
     │     (trigger rebuild if SSG)  │                              │
```

### Why SAS Tokens?

**Advantages:**
- Azure Functions don't proxy large files (saves bandwidth & cost)
- Direct upload to Blob Storage is faster
- Time-limited tokens (5-10 minutes)
- Write-only permissions (can't list or delete)
- Each upload gets unique token (audit trail)

---

## 5. Table Storage Schema Design

### PartitionKey Strategy

**Option 1: Single Partition (Recommended for low traffic)**
- PartitionKey: `"gallery"` (all photos in same partition)
- RowKey: unique ID (GUID or timestamp-based)
- **Pros**: Simple queries, good for < 10,000 photos
- **Cons**: Limited scalability (single partition has throughput limits)

**Option 2: Year-Based Partitioning**
- PartitionKey: `"2026"`, `"2025"`, etc.
- RowKey: unique ID within year
- **Pros**: Better scalability, natural organization
- **Cons**: Queries across years require multiple operations

### Entity Schema

```typescript
interface PhotoEntity {
  // Table Storage required fields
  partitionKey: string;        // "gallery" or year
  rowKey: string;              // Unique ID (GUID)
  timestamp?: Date;            // Auto-managed by Table Storage
  etag?: string;               // Auto-managed (for optimistic concurrency)

  // Custom fields
  blobName: string;            // Blob storage filename
  blobUrl: string;             // Full URL or path
  originalFilename: string;    // Original upload filename
  
  title: string;               // User-provided title
  description: string;         // Optional description
  tags: string;                // Comma-separated or JSON array as string
  
  uploadedAt: string;          // ISO 8601 timestamp
  uploadedBy: string;          // User OID or email
  
  // Image metadata
  contentType: string;         // image/jpeg, image/png
  sizeBytes: number;           // File size
  width?: number;              // Image dimensions (optional)
  height?: number;
  
  // Status fields
  isPublic: boolean;           // Visibility flag
  featured: boolean;           // Featured on homepage
  sortOrder?: number;          // Manual ordering
}
```

### Example Queries

```typescript
// List all public photos (with pagination)
const query = odata`PartitionKey eq 'gallery' and isPublic eq true`;

// Get photos by year (if using year-based partitioning)
const query = odata`PartitionKey eq '2026'`;

// Featured photos only
const query = odata`PartitionKey eq 'gallery' and featured eq true`;
```

---

## 6. Blob Storage Strategy

### Option A: Private Container + SAS Read Tokens (Recommended)

**Setup:**
- Container: `photos` (Private access level)
- Images not publicly accessible by default
- Generate SAS read tokens on-demand when serving gallery

**Advantages:**
- ✅ Full access control
- ✅ Can revoke access by not generating tokens
- ✅ Audit who views what (if logged)
- ✅ Can set expiry times (e.g., 1 hour)

**Disadvantages:**
- ❌ Extra API call to get SAS URL for each image
- ❌ Slight complexity in frontend (need to fetch URLs)
- ❌ SAS tokens in URLs (long, ugly)

**Cost:** Same storage cost, negligible function cost for generating tokens

### Option B: Public Container (Simpler Alternative)

**Setup:**
- Container: `photos` (Blob public read access)
- All images accessible via direct URL
- No tokens needed

**Advantages:**
- ✅ Simple implementation
- ✅ Direct image URLs (can cache, share)
- ✅ No extra API calls

**Disadvantages:**
- ❌ No access control (anyone with URL can view)
- ❌ Cannot revoke access to specific images
- ❌ URLs are predictable (security through obscurity only)

**Cost:** Same as Option A

### Recommendation

**For personal gallery:** Option B (Public Container)
- You want to share photos publicly anyway
- Simpler code, better performance
- Use obscure blob names (UUIDs) for basic security
- Can always migrate to private later

**For sensitive content:** Option A (Private + SAS)

---

## 7. TypeScript Code Examples

### 7.1 Nuxt Auth Composable (`composables/useAuth.ts`)

```typescript
import { PublicClientApplication, type AccountInfo } from '@azure/msal-browser';

interface AuthUser {
  name: string;
  email: string;
  oid: string;
  accessToken: string;
}

export const useAuth = () => {
  const user = useState<AuthUser | null>('auth-user', () => null);
  const isAuthenticated = computed(() => user.value !== null);
  
  let msalInstance: PublicClientApplication | null = null;

  const initMsal = async () => {
    if (msalInstance) return msalInstance;

    const config = useRuntimeConfig();
    
    msalInstance = new PublicClientApplication({
      auth: {
        clientId: config.public.azureClientId,
        authority: `https://login.microsoftonline.com/${config.public.azureTenantId}`,
        redirectUri: config.public.azureRedirectUri,
      },
      cache: {
        cacheLocation: 'localStorage',
        storeAuthStateInCookie: false,
      },
    });

    await msalInstance.initialize();
    return msalInstance;
  };

  const login = async () => {
    const msal = await initMsal();
    
    try {
      const loginResponse = await msal.loginPopup({
        scopes: ['openid', 'profile', 'email'],
        prompt: 'select_account',
      });

      if (loginResponse && loginResponse.account) {
        await setUserFromAccount(loginResponse.account, loginResponse.idToken);
      }
    } catch (error) {
      console.error('Login failed:', error);
      throw error;
    }
  };

  const logout = async () => {
    const msal = await initMsal();
    
    try {
      await msal.logoutPopup();
      user.value = null;
    } catch (error) {
      console.error('Logout failed:', error);
    }
  };

  const checkAuth = async () => {
    const msal = await initMsal();
    const accounts = msal.getAllAccounts();

    if (accounts.length > 0) {
      // Try to get token silently
      try {
        const response = await msal.acquireTokenSilent({
          scopes: ['openid', 'profile', 'email'],
          account: accounts[0],
        });

        await setUserFromAccount(accounts[0], response.idToken);
      } catch (error) {
        console.error('Silent token acquisition failed:', error);
        user.value = null;
      }
    }
  };

  const getAccessToken = async (): Promise<string | null> => {
    const msal = await initMsal();
    const accounts = msal.getAllAccounts();

    if (accounts.length === 0) {
      return null;
    }

    try {
      const response = await msal.acquireTokenSilent({
        scopes: ['openid', 'profile', 'email'],
        account: accounts[0],
      });

      return response.idToken;
    } catch (error) {
      console.error('Failed to get token:', error);
      return null;
    }
  };

  const setUserFromAccount = async (account: AccountInfo, idToken: string) => {
    user.value = {
      name: account.name || '',
      email: account.username || '',
      oid: account.localAccountId,
      accessToken: idToken,
    };
  };

  return {
    user: readonly(user),
    isAuthenticated,
    login,
    logout,
    checkAuth,
    getAccessToken,
  };
};
```

### 7.2 Nuxt API Client (`composables/useApi.ts`)

```typescript
interface ApiError {
  message: string;
  statusCode: number;
}

export const useApi = () => {
  const config = useRuntimeConfig();
  const { getAccessToken } = useAuth();

  const baseUrl = config.public.apiBaseUrl;

  const fetchWithAuth = async <T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> => {
    const token = await getAccessToken();

    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${baseUrl}${endpoint}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      const error: ApiError = {
        message: `API error: ${response.statusText}`,
        statusCode: response.status,
      };
      throw error;
    }

    return response.json();
  };

  const generateSasUrl = async (filename: string, contentType: string) => {
    return fetchWithAuth<{ sasUrl: string; blobName: string }>(
      '/api/generate-sas',
      {
        method: 'POST',
        body: JSON.stringify({ filename, contentType }),
      }
    );
  };

  const saveMetadata = async (metadata: {
    blobName: string;
    originalFilename: string;
    title: string;
    description: string;
    tags: string[];
    contentType: string;
    sizeBytes: number;
  }) => {
    return fetchWithAuth('/api/save-metadata', {
      method: 'POST',
      body: JSON.stringify(metadata),
    });
  };

  const listImages = async (featured?: boolean) => {
    const query = featured ? '?featured=true' : '';
    return fetchWithAuth<{ images: any[] }>(`/api/list-images${query}`);
  };

  const uploadToBlob = async (
    sasUrl: string,
    file: File,
    onProgress?: (progress: number) => void
  ): Promise<void> => {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();

      xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable && onProgress) {
          const progress = (e.loaded / e.total) * 100;
          onProgress(progress);
        }
      });

      xhr.addEventListener('load', () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve();
        } else {
          reject(new Error(`Upload failed: ${xhr.statusText}`));
        }
      });

      xhr.addEventListener('error', () => {
        reject(new Error('Upload failed'));
      });

      xhr.open('PUT', sasUrl);
      xhr.setRequestHeader('x-ms-blob-type', 'BlockBlob');
      xhr.setRequestHeader('Content-Type', file.type);
      xhr.send(file);
    });
  };

  return {
    generateSasUrl,
    saveMetadata,
    listImages,
    uploadToBlob,
  };
};
```

### 7.3 Upload Composable (`composables/useUpload.ts`)

```typescript
import type { Ref } from 'vue';

interface UploadState {
  isUploading: boolean;
  progress: number;
  error: string | null;
}

export const useUpload = () => {
  const state = reactive<UploadState>({
    isUploading: false,
    progress: 0,
    error: null,
  });

  const api = useApi();

  const uploadImage = async (
    file: File,
    metadata: {
      title: string;
      description: string;
      tags: string[];
    }
  ) => {
    state.isUploading = true;
    state.progress = 0;
    state.error = null;

    try {
      // Step 1: Get SAS URL
      state.progress = 10;
      const { sasUrl, blobName } = await api.generateSasUrl(
        file.name,
        file.type
      );

      // Step 2: Upload to blob
      state.progress = 20;
      await api.uploadToBlob(sasUrl, file, (progress) => {
        // Map 20-80% to upload progress
        state.progress = 20 + (progress * 0.6);
      });

      // Step 3: Save metadata
      state.progress = 85;
      await api.saveMetadata({
        blobName,
        originalFilename: file.name,
        title: metadata.title,
        description: metadata.description,
        tags: metadata.tags,
        contentType: file.type,
        sizeBytes: file.size,
      });

      state.progress = 100;
      state.isUploading = false;

      return { success: true, blobName };
    } catch (error: any) {
      state.error = error.message || 'Upload failed';
      state.isUploading = false;
      throw error;
    }
  };

  const reset = () => {
    state.isUploading = false;
    state.progress = 0;
    state.error = null;
  };

  return {
    state: readonly(state),
    uploadImage,
    reset,
  };
};
```

### 7.4 Azure Functions — C# Implementation

> **All Azure Functions are implemented in C# (.NET 8 isolated worker).**
> The full source lives under `photo-functions/src/PhotoFunctions/`.
> Below is a summary of each function — see the actual `.cs` files for the
> complete, production-ready code.

#### Function endpoints

| Function | Route | Auth | Description |
|----------|-------|------|-------------|
| `auth-validate` | `POST /api/auth-validate` | Bearer JWT | Validates token & checks OID allowlist |
| `generate-sas` | `POST /api/generate-sas` | Bearer JWT | Returns time-limited SAS upload URL |
| `save-metadata` | `POST /api/save-metadata` | Bearer JWT | Inserts photo metadata into Table Storage |
| `list-images` | `GET /api/list-images` | Public | Returns all public images (optionally featured) |
| `health` | `GET /api/health` | Public | Health-check endpoint |

#### Key design highlights

- **Dependency Injection** via `Program.cs` — services are registered as singletons.
- **Options pattern** — `AzureAdOptions` and `AzureStorageOptions` are bound from configuration.
- **JWT validation** uses `Microsoft.IdentityModel` with OIDC discovery (auto key-rotation).
- **Authorization** checks the `oid` claim against the `ALLOWED_USER_OIDS` env var.
- **SAS tokens** are generated with `Azure.Storage.Blobs` using `StorageSharedKeyCredential`.
- The `ListImagesFunction` transparently supports both private (SAS read URLs) and public containers via the `UsePrivateContainer` setting.

*The TypeScript examples from the original draft have been replaced by the C# files. The Nuxt front-end composables (sections 7.1–7.3 above) remain in TypeScript.*

---

**REMOVED — the following TypeScript blocks are superseded by C# source files:**

~~7.4 auth-validate.ts~~ → `Functions/AuthValidateFunction.cs` + `Services/JwtValidationService.cs`
~~7.5 generate-sas.ts~~ → `Functions/GenerateSasFunction.cs` + `Services/BlobStorageService.cs`
~~7.6 save-metadata.ts~~ → `Functions/SaveMetadataFunction.cs` + `Services/PhotoTableService.cs`
~~7.7 list-images.ts~~ → `Functions/ListImagesFunction.cs`

<!--  OLD TYPESCRIPT BLOCKS REMOVED — kept as HTML comment for reference

interface JwtPayload {
  oid: string;
  email: string;
  name: string;
  aud: string;
  iss: string;
  exp: number;
}

const client = jwksClient({
  jwksUri: `https://login.microsoftonline.com/${process.env.AZURE_TENANT_ID}/discovery/v2.0/keys`,
  cache: true,
  cacheMaxAge: 86400000, // 24 hours
});

const getSigningKey = (header: any, callback: any) => {
  client.getSigningKey(header.kid, (err, key) => {
    if (err) {
      callback(err);
      return;
    }
    const signingKey = key?.getPublicKey();
    callback(null, signingKey);
  });
};

export const validateJwt = async (token: string): Promise<JwtPayload> => {
  return new Promise((resolve, reject) => {
    jwt.verify(
      token,
      getSigningKey,
      {
        audience: process.env.AZURE_CLIENT_ID,
        issuer: `https://login.microsoftonline.com/${process.env.AZURE_TENANT_ID}/v2.0`,
        algorithms: ['RS256'],
      },
      (err, decoded) => {
        if (err) {
          reject(err);
          return;
        }
        resolve(decoded as JwtPayload);
      }
    );
  });
};

export const isAuthorizedUser = (oid: string): boolean => {
  const allowedUsers = process.env.ALLOWED_USER_OIDS?.split(',') || [];
  return allowedUsers.includes(oid);
};

export const extractToken = (req: HttpRequest): string | null => {
  const authHeader = req.headers.get('authorization');
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return null;
  }
  return authHeader.substring(7);
};

app.http('auth-validate', {
  methods: ['POST'],
  authLevel: 'anonymous',
  handler: async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    try {
      const token = extractToken(req);
      if (!token) {
        return {
          status: 401,
          jsonBody: { error: 'No token provided' },
        };
      }

      const payload = await validateJwt(token);

      if (!isAuthorizedUser(payload.oid)) {
        return {
          status: 403,
          jsonBody: { error: 'User not authorized' },
        };
      }

      return {
        status: 200,
        jsonBody: {
          valid: true,
          user: {
            oid: payload.oid,
            email: payload.email,
            name: payload.name,
          },
        },
      };
    } catch (error: any) {
      context.error('JWT validation failed:', error);
      return {
        status: 401,
        jsonBody: { error: 'Invalid token', details: error.message },
      };
    }
  },
});
```

### 7.5 Azure Function: Generate SAS URL (`functions/generate-sas.ts`)

```typescript
import { app, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import { BlobServiceClient, generateBlobSASQueryParameters, BlobSASPermissions, StorageSharedKeyCredential } from '@azure/storage-blob';
import { v4 as uuidv4 } from 'uuid';
import { validateJwt, isAuthorizedUser, extractToken } from './auth-validate';

const accountName = process.env.AZURE_STORAGE_ACCOUNT_NAME!;
const accountKey = process.env.AZURE_STORAGE_ACCOUNT_KEY!;
const containerName = 'photos';

const sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
const blobServiceClient = new BlobServiceClient(
  `https://${accountName}.blob.core.windows.net`,
  sharedKeyCredential
);

interface GenerateSasRequest {
  filename: string;
  contentType: string;
}

app.http('generate-sas', {
  methods: ['POST'],
  authLevel: 'anonymous',
  handler: async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    try {
      // Validate JWT
      const token = extractToken(req);
      if (!token) {
        return { status: 401, jsonBody: { error: 'No token provided' } };
      }

      const payload = await validateJwt(token);
      if (!isAuthorizedUser(payload.oid)) {
        return { status: 403, jsonBody: { error: 'User not authorized' } };
      }

      // Parse request
      const body = await req.json() as GenerateSasRequest;
      if (!body.filename || !body.contentType) {
        return { status: 400, jsonBody: { error: 'Missing filename or contentType' } };
      }

      // Generate unique blob name
      const extension = body.filename.split('.').pop();
      const blobName = `${uuidv4()}.${extension}`;

      // Create SAS token (write only, 10 minutes expiry)
      const containerClient = blobServiceClient.getContainerClient(containerName);
      const blobClient = containerClient.getBlobClient(blobName);

      const sasOptions = {
        containerName,
        blobName,
        permissions: BlobSASPermissions.parse('w'), // write only
        startsOn: new Date(),
        expiresOn: new Date(Date.now() + 10 * 60 * 1000), // 10 minutes
        contentType: body.contentType,
      };

      const sasToken = generateBlobSASQueryParameters(
        sasOptions,
        sharedKeyCredential
      ).toString();

      const sasUrl = `${blobClient.url}?${sasToken}`;

      context.log(`Generated SAS URL for blob: ${blobName}`);

      return {
        status: 200,
        jsonBody: {
          sasUrl,
          blobName,
          expiresIn: 600, // seconds
        },
      };
    } catch (error: any) {
      context.error('Failed to generate SAS URL:', error);
      return {
        status: 500,
        jsonBody: { error: 'Failed to generate SAS URL', details: error.message },
      };
    }
  },
});
```

### 7.6 Azure Function: Save Metadata (`functions/save-metadata.ts`)

```typescript
import { app, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import { TableClient, AzureNamedKeyCredential } from '@azure/data-tables';
import { v4 as uuidv4 } from 'uuid';
import { validateJwt, isAuthorizedUser, extractToken } from './auth-validate';

const accountName = process.env.AZURE_STORAGE_ACCOUNT_NAME!;
const accountKey = process.env.AZURE_STORAGE_ACCOUNT_KEY!;
const tableName = 'photos';

const credential = new AzureNamedKeyCredential(accountName, accountKey);
const tableClient = new TableClient(
  `https://${accountName}.table.core.windows.net`,
  tableName,
  credential
);

interface SaveMetadataRequest {
  blobName: string;
  originalFilename: string;
  title: string;
  description: string;
  tags: string[];
  contentType: string;
  sizeBytes: number;
  width?: number;
  height?: number;
}

app.http('save-metadata', {
  methods: ['POST'],
  authLevel: 'anonymous',
  handler: async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    try {
      // Validate JWT
      const token = extractToken(req);
      if (!token) {
        return { status: 401, jsonBody: { error: 'No token provided' } };
      }

      const payload = await validateJwt(token);
      if (!isAuthorizedUser(payload.oid)) {
        return { status: 403, jsonBody: { error: 'User not authorized' } };
      }

      // Parse request
      const body = await req.json() as SaveMetadataRequest;
      
      // Validate required fields
      if (!body.blobName || !body.originalFilename || !body.contentType) {
        return {
          status: 400,
          jsonBody: { error: 'Missing required fields' },
        };
      }

      // Create entity
      const rowKey = uuidv4();
      const blobUrl = `https://${accountName}.blob.core.windows.net/photos/${body.blobName}`;

      const entity = {
        partitionKey: 'gallery', // Single partition strategy
        rowKey,
        blobName: body.blobName,
        blobUrl,
        originalFilename: body.originalFilename,
        title: body.title || body.originalFilename,
        description: body.description || '',
        tags: body.tags.join(','), // Store as comma-separated string
        uploadedAt: new Date().toISOString(),
        uploadedBy: payload.oid,
        contentType: body.contentType,
        sizeBytes: body.sizeBytes,
        width: body.width || 0,
        height: body.height || 0,
        isPublic: true,
        featured: false,
        sortOrder: 0,
      };

      await tableClient.createEntity(entity);

      context.log(`Saved metadata for blob: ${body.blobName}`);

      return {
        status: 201,
        jsonBody: {
          success: true,
          id: rowKey,
          blobUrl,
        },
      };
    } catch (error: any) {
      context.error('Failed to save metadata:', error);
      return {
        status: 500,
        jsonBody: { error: 'Failed to save metadata', details: error.message },
      };
    }
  },
});
```

### 7.7 Azure Function: List Images (`functions/list-images.ts`)

```typescript
import { app, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import { TableClient, AzureNamedKeyCredential, odata } from '@azure/data-tables';
import { generateBlobSASQueryParameters, BlobSASPermissions, StorageSharedKeyCredential } from '@azure/storage-blob';

const accountName = process.env.AZURE_STORAGE_ACCOUNT_NAME!;
const accountKey = process.env.AZURE_STORAGE_ACCOUNT_KEY!;
const tableName = 'photos';
const usePrivateContainer = process.env.USE_PRIVATE_CONTAINER === 'true';

const credential = new AzureNamedKeyCredential(accountName, accountKey);
const tableClient = new TableClient(
  `https://${accountName}.table.core.windows.net`,
  tableName,
  credential
);

const sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);

const generateReadSasToken = (blobName: string): string => {
  const sasOptions = {
    containerName: 'photos',
    blobName,
    permissions: BlobSASPermissions.parse('r'), // read only
    startsOn: new Date(),
    expiresOn: new Date(Date.now() + 60 * 60 * 1000), // 1 hour
  };

  return generateBlobSASQueryParameters(sasOptions, sharedKeyCredential).toString();
};

app.http('list-images', {
  methods: ['GET'],
  authLevel: 'anonymous',
  handler: async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    try {
      const featuredOnly = req.query.get('featured') === 'true';
      
      // Build query
      let query = odata`PartitionKey eq 'gallery' and isPublic eq true`;
      if (featuredOnly) {
        query = odata`PartitionKey eq 'gallery' and isPublic eq true and featured eq true`;
      }

      // Fetch entities
      const entities = tableClient.listEntities({ queryOptions: { filter: query } });
      
      const images = [];
      for await (const entity of entities) {
        let imageUrl = entity.blobUrl as string;

        // If using private container, append SAS token
        if (usePrivateContainer) {
          const sasToken = generateReadSasToken(entity.blobName as string);
          imageUrl = `${imageUrl}?${sasToken}`;
        }

        images.push({
          id: entity.rowKey,
          blobName: entity.blobName,
          url: imageUrl,
          title: entity.title,
          description: entity.description,
          tags: (entity.tags as string).split(',').filter(t => t),
          uploadedAt: entity.uploadedAt,
          width: entity.width,
          height: entity.height,
          sizeBytes: entity.sizeBytes,
          featured: entity.featured,
        });
      }

      // Sort by uploadedAt (newest first)
      images.sort((a, b) => 
        new Date(b.uploadedAt).getTime() - new Date(a.uploadedAt).getTime()
      );

      context.log(`Listed ${images.length} images`);

      return {
        status: 200,
        jsonBody: { images },
      };
    } catch (error: any) {
      context.error('Failed to list images:', error);
      return {
        status: 500,
        jsonBody: { error: 'Failed to list images', details: error.message },
      };
    }
  },
});
```
-->

---

## 8. Environment Variables

### Nuxt 3 (`.env` and `.env.production`)

```bash
# Azure AD (Microsoft Entra ID)
NUXT_PUBLIC_AZURE_CLIENT_ID=your-client-id-here
NUXT_PUBLIC_AZURE_TENANT_ID=your-tenant-id-here
NUXT_PUBLIC_AZURE_REDIRECT_URI=https://yourdomain.z13.web.core.windows.net/auth/callback

# Azure Functions API
NUXT_PUBLIC_API_BASE_URL=https://your-function-app.azurewebsites.net

# Optional: Analytics, etc.
NUXT_PUBLIC_SITE_URL=https://yourdomain.z13.web.core.windows.net
```

### Azure Functions (C# — `local.settings.json`)

The .NET isolated worker uses `__` (double underscore) as a hierarchy separator
that maps to `IConfiguration` sections. These map directly to the Options
classes (`AzureStorageOptions` / `AzureAdOptions`).

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    "AzureStorage__AccountName": "yourstorageaccount",
    "AzureStorage__AccountKey": "your-storage-key-here",
    "AzureStorage__PhotoContainerName": "photos",
    "AzureStorage__PhotoTableName": "photos",
    "AzureStorage__UsePrivateContainer": "false",

    "AzureAd__TenantId": "your-tenant-id-here",
    "AzureAd__ClientId": "your-client-id-here",
    "AzureAd__AllowedUserOids": "your-user-oid-1,your-user-oid-2"
  },
  "Host": {
    "CORS": "*",
    "CORSCredentials": false
  }
}
```

### Production (Azure Function App Settings)

Configure these as Application Settings in Azure Portal (using `__` separator):

| Setting | Example |
|---------|---------|
| `AzureStorage__AccountName` | `photogallerystore` |
| `AzureStorage__AccountKey` | `<your-key>` |
| `AzureStorage__PhotoContainerName` | `photos` |
| `AzureStorage__PhotoTableName` | `photos` |
| `AzureStorage__UsePrivateContainer` | `false` |
| `AzureAd__TenantId` | `<your-tenant-id>` |
| `AzureAd__ClientId` | `<your-client-id>` |
| `AzureAd__AllowedUserOids` | `<your-oid>` |

---

## 9. Deployment Steps

### 9.1 Initial Azure Setup

```bash
# Variables
RESOURCE_GROUP="photo-gallery-rg"
LOCATION="switzerlandNorth"
STORAGE_ACCOUNT="photogallerystore"  # Must be globally unique
FUNCTION_APP="photogallery-api"      # Must be globally unique
TABLE_NAME="photos"
CONTAINER_NAME="photos"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create storage account (for static website + blobs + tables)
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2

# Enable static website
az storage blob service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --static-website \
  --index-document index.html \
  --404-document 404.html

# Create blob container for images
az storage container create \
  --name $CONTAINER_NAME \
  --account-name $STORAGE_ACCOUNT \
  --public-access blob  # or 'off' for private

# Create table for metadata
az storage table create \
  --name $TABLE_NAME \
  --account-name $STORAGE_ACCOUNT

# Get storage account key
az storage account keys list \
  --resource-group $RESOURCE_GROUP \
  --account-name $STORAGE_ACCOUNT \
  --query "[0].value" --output tsv
```

### 9.2 Create Azure Function App

```bash
# Create function app (consumption plan, .NET 8 isolated)
az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $LOCATION \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --name $FUNCTION_APP \
  --storage-account $STORAGE_ACCOUNT

# Configure CORS
az functionapp cors add \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "https://${STORAGE_ACCOUNT}.z13.web.core.windows.net"

# Set application settings (note the __ hierarchy separator)
az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --settings \
    AzureStorage__AccountName=$STORAGE_ACCOUNT \
    AzureStorage__AccountKey="<your-key>" \
    AzureStorage__PhotoContainerName="photos" \
    AzureStorage__PhotoTableName="photos" \
    AzureStorage__UsePrivateContainer="false" \
    AzureAd__TenantId="<your-tenant-id>" \
    AzureAd__ClientId="<your-client-id>" \
    AzureAd__AllowedUserOids="<your-oid>"
```

### 9.3 Configure Microsoft Entra ID

```bash
# Create app registration
az ad app create \
  --display-name "PhotoGallery" \
  --sign-in-audience AzureADMyOrg \
  --web-redirect-uris "https://${STORAGE_ACCOUNT}.z13.web.core.windows.net/auth/callback" \
  --enable-id-token-issuance true

# Get your user OID (for ALLOWED_USER_OIDS)
az ad signed-in-user show --query id -o tsv
```

**Manual steps in Azure Portal:**
1. Go to App Registration → Authentication
2. Add platform: Single-page application
3. Add redirect URI
4. Enable ID tokens
5. Copy Client ID and Tenant ID

### 9.4 Deploy Nuxt Application

```bash
cd photo-gallery

# Install dependencies
npm install

# Create production environment file
cat > .env.production << EOF
NUXT_PUBLIC_AZURE_CLIENT_ID=your-client-id
NUXT_PUBLIC_AZURE_TENANT_ID=your-tenant-id
NUXT_PUBLIC_AZURE_REDIRECT_URI=https://${STORAGE_ACCOUNT}.z13.web.core.windows.net/auth/callback
NUXT_PUBLIC_API_BASE_URL=https://${FUNCTION_APP}.azurewebsites.net
EOF

# Generate static site
npm run generate

# Deploy to Azure Storage
az storage blob upload-batch \
  --account-name $STORAGE_ACCOUNT \
  --destination '$web' \
  --source .output/public \
  --overwrite

# Get static website URL
echo "Website URL: https://${STORAGE_ACCOUNT}.z13.web.core.windows.net"
```

### 9.5 Deploy Azure Functions (C# / .NET 8)

```bash
cd photo-functions/src/PhotoFunctions

# Restore & build
dotnet restore
dotnet build --configuration Release

# Publish (creates self-contained output)
dotnet publish --configuration Release --output ./publish

# Deploy to Azure (from the publish folder)
func azure functionapp publish $FUNCTION_APP

# — OR — deploy with Azure CLI directly:
cd publish
az functionapp deployment source config-zip \
  --resource-group $RESOURCE_GROUP \
  --name $FUNCTION_APP \
  --src ./publish.zip

# Test deployment
curl https://${FUNCTION_APP}.azurewebsites.net/api/health
curl https://${FUNCTION_APP}.azurewebsites.net/api/list-images
```

### 9.6 CI/CD with GitHub Actions (Optional)

```yaml
# .github/workflows/deploy.yml
name: Deploy Photo Gallery

on:
  push:
    branches: [main]

jobs:
  deploy-nuxt:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'
      
      - name: Install dependencies
        run: npm ci
        working-directory: ./photo-gallery
      
      - name: Generate static site
        run: npm run generate
        working-directory: ./photo-gallery
        env:
          NUXT_PUBLIC_AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
          NUXT_PUBLIC_AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
          NUXT_PUBLIC_AZURE_REDIRECT_URI: ${{ secrets.REDIRECT_URI }}
          NUXT_PUBLIC_API_BASE_URL: ${{ secrets.API_BASE_URL }}
      
      - name: Deploy to Azure Storage
        uses: azure/CLI@v1
        with:
          inlineScript: |
            az storage blob upload-batch \
              --account-name ${{ secrets.STORAGE_ACCOUNT }} \
              --destination '$web' \
              --source ./photo-gallery/.output/public \
              --overwrite

  deploy-functions:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore & Publish
        run: |
          dotnet restore
          dotnet publish --configuration Release --output ./publish
        working-directory: ./photo-functions/src/PhotoFunctions

      - name: Deploy to Azure Functions
        uses: Azure/functions-action@v1
        with:
          app-name: ${{ secrets.FUNCTION_APP_NAME }}
          package: ./photo-functions/src/PhotoFunctions/publish
          publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

---

## 10. Security Best Practices

### 10.1 Authentication & Authorization

✅ **Use single-tenant Azure AD**
- Restrict to your organization only
- Don't allow personal Microsoft accounts

✅ **Validate JWT on every protected endpoint**
- Check signature, issuer, audience, expiry
- Verify user OID against allowlist

✅ **Store allowed user OIDs in environment variables**
```typescript
const allowedUsers = process.env.ALLOWED_USER_OIDS?.split(',') || [];
```

✅ **Never expose storage account keys in client**
- Only use SAS tokens generated server-side
- Tokens should be short-lived (5-10 minutes)

### 10.2 Storage Security

✅ **Use HTTPS everywhere**
- Enforce HTTPS on storage account
- Use `https://` URLs only

✅ **Set minimal SAS token permissions**
- Upload: write-only (`w`)
- Download: read-only (`r`)
- Never grant delete or list permissions

✅ **Use unique blob names (UUIDs)**
- Prevents enumeration attacks
- Even with public container, URLs aren't guessable

✅ **Enable Azure Storage logging**
- Track who uploads/downloads
- Set up alerts for unusual activity

### 10.3 Function App Security

✅ **Set `authLevel: 'anonymous'` but validate JWT manually**
- More flexible than Azure AD built-in auth
- Better error messages

✅ **Enable Application Insights**
- Monitor function executions
- Alert on errors

✅ **Set CORS properly**
```typescript
// Only allow your domain
allowedOrigins: ['https://yourdomain.z13.web.core.windows.net']
```

✅ **Use managed identities (advanced)**
- Avoid storing storage keys in Function App settings
- Grant Function App managed identity access to storage

### 10.4 Client-Side Security

✅ **Validate file types before upload**
```typescript
const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
if (!allowedTypes.includes(file.type)) {
  throw new Error('Invalid file type');
}
```

✅ **Limit file sizes**
```typescript
const MAX_SIZE = 10 * 1024 * 1024; // 10 MB
if (file.size > MAX_SIZE) {
  throw new Error('File too large');
}
```

✅ **Don't trust client-provided metadata**
- Validate on server side
- Sanitize titles, descriptions, tags

### 10.5 Network Security

✅ **Restrict storage account network access (optional)**
- Allow only Azure Functions IP ranges
- Requires premium plan or Service Endpoints

✅ **Use Azure Front Door or CDN**
- DDoS protection
- WAF rules
- HTTPS enforcement

---

## 11. Cost Optimization

### 11.1 Estimated Monthly Costs (Low Traffic)

Assuming:
- 100 photo uploads/month
- 1,000 page views/month
- 10 GB storage
- 1,000 Azure Function executions/month

| Service | Tier | Cost |
|---------|------|------|
| **Azure Storage Account** | Standard LRS | ~$0.50 |
| - Static website hosting | Free | $0 |
| - Blob storage (10 GB) | $0.018/GB | $0.18 |
| - Table storage (< 1 GB) | $0.045/GB | $0.05 |
| - Operations (read/write) | Minimal | $0.10 |
| **Azure Functions** | Consumption | ~$0.20 |
| - Executions (1M free/month) | $0.20/million | $0 |
| - Execution time (400K GB-s free) | Minimal | $0.20 |
| **Bandwidth** | First 100 GB free | $0 |
| **Total** | | **~$0.70 - $1/month** |

### 11.2 Cost Optimization Tips

✅ **Use Consumption Plan for Functions**
- Pay only for executions
- Auto-scales to zero

✅ **Use LRS (Locally Redundant Storage)**
- Cheaper than GRS/ZRS
- Sufficient for personal projects

✅ **Optimize blob storage**
- Compress images before upload (client-side)
- Use WebP format (smaller than JPEG)
- Set lifecycle policy to delete old logs

```bash
# Example: Move old blobs to cool tier after 30 days
az storage account management-policy create \
  --account-name $STORAGE_ACCOUNT \
  --policy @lifecycle-policy.json
```

**lifecycle-policy.json:**
```json
{
  "rules": [
    {
      "enabled": true,
      "name": "archive-old-logs",
      "type": "Lifecycle",
      "definition": {
        "actions": {
          "baseBlob": {
            "tierToCool": {
              "daysAfterModificationGreaterThan": 30
            }
          }
        },
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["logs/"]
        }
      }
    }
  ]
}
```

✅ **Cache static assets**
- Nuxt generates cache headers automatically
- Browser caches `_nuxt/*` files

✅ **Use Azure CDN only if traffic grows**
- Not cost-effective for < 1,000 views/month
- Consider Cloudflare (free tier)

✅ **Disable Application Insights sampling in prod**
- Or use 10% sampling to reduce costs
- Only enable full logging for debugging

✅ **Monitor costs**
```bash
# Set up budget alert
az consumption budget create \
  --resource-group $RESOURCE_GROUP \
  --budget-name photo-gallery-budget \
  --amount 10 \
  --category Cost \
  --time-grain Monthly \
  --start-date $(date +%Y-%m-01) \
  --end-date 2027-12-31
```

---

## 12. Recommended Improvements

### 12.1 Progressive Enhancements

**Phase 1: MVP (What's described above)**
- Basic upload/view functionality
- OAuth authentication
- Static generation

**Phase 2: Enhanced UX**
- Image thumbnails (generate on upload)
- Lazy loading with intersection observer
- Lightbox/gallery viewer
- Search and filter by tags

**Phase 3: Advanced Features**
- Image processing (resize, crop) with Azure Functions
- Automatic thumbnail generation
- EXIF data extraction (GPS, camera info)
- Albums/collections
- Share individual photos (temporary SAS URLs)

**Phase 4: Performance**
- Azure CDN for image delivery
- WebP conversion (Azure Functions + Sharp)
- Progressive JPEG encoding
- Service Worker for offline viewing

### 12.2 Monitoring & Maintenance

✅ **Set up Application Insights dashboards**
- Function execution times
- Error rates
- Storage operations

✅ **Create runbook for common tasks**
- How to add new allowed users
- How to delete photos (blob + table entry)
- How to regenerate after SSG changes

✅ **Automate SSG regeneration**
- Trigger Nuxt rebuild on new upload (via webhook)
- Or schedule nightly rebuild (GitHub Actions cron)

---

## 13. File Generation vs. Dynamic Pages

### Current Approach: Full Static (SSG)

**Pros:**
- Zero server costs for page hosting
- Lightning fast (pre-rendered HTML)
- Simple deployment (just files)

**Cons:**
- Must rebuild after every upload
- Gallery not immediately updated

### Alternative: Hybrid (SSG + Client Fetch)

Generate static pages for layout, fetch images dynamically via API:

```vue
<!-- pages/index.vue -->
<script setup lang="ts">
const { listImages } = useApi();
const { data: images } = await useAsyncData('images', () => listImages());
</script>
```

**Pros:**
- Gallery updates immediately after upload
- No rebuild needed

**Cons:**
- Requires client-side JavaScript
- Initial load slower (API call)

**Recommendation for your use case:** Hybrid approach
- Pre-render layout/shell
- Fetch images on mount
- Best of both worlds (fast load + live updates)

---

## 14. Example `nuxt.config.ts`

```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  ssr: true,
  
  // Generate static site
  nitro: {
    preset: 'static',
  },

  app: {
    head: {
      title: 'Nature & Garden Gallery',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'Personal nature and garden photography gallery' },
      ],
      link: [
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
      ],
    },
  },

  css: ['~/assets/css/main.css'],

  modules: [
    '@nuxtjs/tailwindcss', // Optional: for styling
  ],

  runtimeConfig: {
    // Private keys (server-side only)
    // none for this project (all public)

    // Public keys (exposed to client)
    public: {
      azureClientId: process.env.NUXT_PUBLIC_AZURE_CLIENT_ID,
      azureTenantId: process.env.NUXT_PUBLIC_AZURE_TENANT_ID,
      azureRedirectUri: process.env.NUXT_PUBLIC_AZURE_REDIRECT_URI,
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL,
      siteUrl: process.env.NUXT_PUBLIC_SITE_URL,
    },
  },

  typescript: {
    strict: true,
    typeCheck: true,
  },

  devtools: { enabled: true },
});
```

---

## 15. Example Package.json Files

### Nuxt 3 (`photo-gallery/package.json`)

```json
{
  "name": "photo-gallery",
  "version": "1.0.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "nuxt dev",
    "build": "nuxt build",
    "generate": "nuxt generate",
    "preview": "nuxt preview",
    "lint": "eslint .",
    "typecheck": "nuxt typecheck"
  },
  "dependencies": {
    "@azure/msal-browser": "^3.7.0",
    "nuxt": "^3.9.0",
    "vue": "^3.4.0"
  },
  "devDependencies": {
    "@nuxtjs/tailwindcss": "^6.11.0",
    "@typescript-eslint/eslint-plugin": "^6.18.0",
    "@typescript-eslint/parser": "^6.18.0",
    "eslint": "^8.56.0",
    "typescript": "^5.3.0"
  }
}
```

### Azure Functions (`photo-functions/src/PhotoFunctions/PhotoFunctions.csproj`)

The C# project uses the following NuGet packages:

| Package | Purpose |
|---------|---------|
| `Microsoft.Azure.Functions.Worker` | .NET isolated worker runtime |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | HTTP trigger binding |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | JSON serialization helpers |
| `Azure.Data.Tables` | Table Storage client |
| `Azure.Storage.Blobs` | Blob Storage client + SAS generation |
| `System.IdentityModel.Tokens.Jwt` | JWT token validation |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect` | OIDC discovery for key rotation |

See `PhotoFunctions.csproj` for exact versions.

---

## 16. Quick Start Commands

```bash
# 1. Clone/create project
mkdir PhotoPaccots && cd PhotoPaccots

# 2. Create Nuxt app
npx nuxi@latest init photo-gallery
cd photo-gallery
npm install @azure/msal-browser

# 3. Azure Functions project (already scaffolded as C#)
#    The project lives at photo-functions/src/PhotoFunctions/
cd ../photo-functions/src/PhotoFunctions
dotnet restore

# 4. Set up Azure resources (use az commands from section 9.1)

# 5. Configure environment variables
#    - photo-gallery/.env (see section 8)
#    - photo-functions/src/PhotoFunctions/local.settings.json (see section 8)

# 6. Develop locally
cd photo-gallery
npm run dev                   # http://localhost:3000

cd photo-functions/src/PhotoFunctions
func start                    # http://localhost:7071

# 7. Deploy
cd photo-gallery
npm run generate
az storage blob upload-batch --account-name $STORAGE_ACCOUNT \
  --destination '$web' --source .output/public --overwrite

cd photo-functions/src/PhotoFunctions
dotnet publish -c Release -o ./publish
func azure functionapp publish $FUNCTION_APP
```

---

## Summary

This architecture provides:

✅ **Low cost** (~$1/month for low traffic)  
✅ **Secure** (OAuth, JWT validation, SAS tokens)  
✅ **Scalable** (consumption plan, static hosting)  
✅ **Modern** (Nuxt 3 + TypeScript front-end, C# .NET 8 back-end)  
✅ **Simple** (minimal moving parts, no overengineering)  

**Key Design Decisions:**

1. **Public blob container** (recommended for simplicity)
2. **Single partition** in Table Storage (suitable for < 10K photos)
3. **Hybrid rendering** (SSG shell + client-side data fetch)
4. **SAS tokens for uploads** (secure, direct to storage)
5. **JWT validation in Functions** (not Azure AD built-in auth)

The architecture is production-ready yet simple enough for a personal project. You can start with MVP and progressively enhance as needed.
