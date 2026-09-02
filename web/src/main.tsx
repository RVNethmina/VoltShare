// -----------------------------------------------------------------------------
// File        : main.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Browser entry point. Mounts the React tree and installs the
//               router and the authentication provider around it.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './context/AuthContext'
import './index.css'

const container = document.getElementById('root')

// Fail loudly rather than silently rendering nothing if the host element is
// ever renamed in index.html.
if (!container) {
  throw new Error('Root element #root was not found in index.html.')
}

createRoot(container).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
)
