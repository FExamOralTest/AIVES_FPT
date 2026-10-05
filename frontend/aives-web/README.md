# aives-web

Frontend for AIVES, the AI viva exam system. Built with React, TypeScript, Vite, Tailwind CSS v4 and shadcn/ui.

## Run

```bash
npm install
npm run dev
```

The dev server proxies `/api` to the backend at `https://localhost:7223` (set `VITE_API_PROXY_TARGET` to change it). For a deployed backend, set `VITE_API_BASE_URL`. See `.env.example`.

## Structure (feature-based)

```text
src/
├── app/                 App shell: providers, routes, 404
├── assets/images/       Static images
├── components/
│   ├── ui/              shadcn/ui components (add more with `npx shadcn@latest add <name>`)
│   └── common/          Shared brand pieces: Logo, Waveform, DotGrid
├── features/
│   ├── auth/            Login, register, session, route guards  (public API: index.ts)
│   ├── landing/         Public landing page and its sections
│   └── dashboard/       Signed-in home per role
├── lib/
│   ├── http/            Axios client and API error mapping
│   └── utils.ts         `cn()` class helper
├── index.css            Tailwind + theme tokens (brand colors, fonts, radius)
└── main.tsx
```

Rules of thumb:

- A feature owns its `api/`, `components/`, `hooks/`, `lib/`, `pages/` and `types.ts`.
- Other code imports a feature only through its `index.ts`.
- `lib/` and `components/` never import from `features/`.
- Theme colors live in `src/index.css`: `brand` (#0F6CE6), `logo` (#6AA8FF), `sun` (#FFD54A).
