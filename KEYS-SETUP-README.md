# Keys and URLs Setup (External APIs Only)

This page lists only non-Azure API keys and URLs used by Photo Paccots.

## Front-end (`photo-paccots/.env`)

| Variable | Where to get it |
|---|---|
| `NUXT_PUBLIC_TURNSTILE_SITE_KEY` | Cloudflare Turnstile site key in [Cloudflare Dashboard](https://dash.cloudflare.com/) |

## Back-end (API settings)

| Setting | Where to get it |
|---|---|
| `PlantNet__ApiKey` | PlantNet API key from [my.plantnet.org](https://my.plantnet.org/) |
| `BirdApi__ApiKey` | RapidAPI key from [RapidAPI](https://rapidapi.com/) |
| `BirdApi__Host` | `bird-classifier.p.rapidapi.com` |
| `BirdApi__BaseUrl` | `https://bird-classifier.p.rapidapi.com` |
| `BirdApi__Path` | `/BirdClassifier/prediction` |
| `Gemini__ApiKey` | Google AI Studio key from [aistudio.google.com](https://aistudio.google.com/app/apikey) |
| `Gemini__BaseUrl` | `https://generativelanguage.googleapis.com` |
| `Turnstile__SiteKey` | Cloudflare Turnstile site key in [Cloudflare Dashboard](https://dash.cloudflare.com/) |
| `Turnstile__SecretKey` | Cloudflare Turnstile secret key in [Cloudflare Dashboard](https://dash.cloudflare.com/) |


