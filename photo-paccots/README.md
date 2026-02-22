# Photo Paccots — Front-end

Nuxt 3 static site for the Photo Paccots gallery.

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
