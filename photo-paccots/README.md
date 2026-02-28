# Photo Paccots — Front-end

Nuxt 3 static site for the Photo Paccots gallery.

## Current Features

- Public gallery with folder navigation (`Species` and `Year/Month` modes)
- Folder-aware display:
  - slideshow + child folder cards at root/intermediate levels
  - full image grid at leaf level
- Configurable slideshow behavior (photo count, interval, fade transition)
- Upload and management pages for authenticated users
- Translation management with per-key lock/unlock for auto-translation
- Public `Thanks to:` page available from footer (`/powered-by`)

## Main Pages

- `/` — Gallery
- `/upload` — Upload (auth required)
- `/manage` — Image management (auth required)
- `/manage-translations` — Translation management (auth required)
- `/manage-settings` — Slideshow settings (auth required)
- `/powered-by` — Public credits/thanks page

## Setup

```bash
npm install
cp .env.example .env
# Fill in the values in .env
```

## Development

```bash
npm run dev
```

Runs at `http://localhost:3000`.

## Static Generation

```bash
npm run generate
```

Output is in `.output/public/`. Deploy to Azure Storage using `5_Deploy_nuxt.ps1`.

## Required Environment Variables

- `NUXT_PUBLIC_AZURE_CLIENT_ID`
- `NUXT_PUBLIC_AZURE_TENANT_ID`
- `NUXT_PUBLIC_AZURE_REDIRECT_URI`
- `NUXT_PUBLIC_API_BASE_URL`
- `NUXT_PUBLIC_SITE_URL` (optional but recommended)
