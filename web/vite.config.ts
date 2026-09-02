// -----------------------------------------------------------------------------
// File        : vite.config.ts
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Build configuration for the React back-office application.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  // Tailwind v4 is wired in as a Vite plugin, so no separate config file or
  // PostCSS pipeline is needed.
  plugins: [react(), tailwindcss()],

  server: {
    port: 5173,
    // Listen on every interface so the site can be opened from another device
    // on the same network during the demonstration.
    host: true,
  },

  build: {
    outDir: 'dist',
    sourcemap: false,
  },
})
