// -----------------------------------------------------------------------------
// File        : components/ThemeToggle.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Three way switch between the light theme, the dark theme and
//               following the operating system.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useTheme } from '../context/useTheme'
import type { ThemePreference } from '../context/ThemeContext'

interface Option {
  value: ThemePreference
  label: string
  icon: string
}

const OPTIONS: Option[] = [
  { value: 'light', label: 'Light theme', icon: '☀' },
  { value: 'dark', label: 'Dark theme', icon: '☾' },
  { value: 'system', label: 'Match system', icon: '⌘' },
]

/**
 * A small segmented control. Showing all three choices at once makes the
 * current setting obvious, which a single cycling button does not.
 */
export default function ThemeToggle() {
  const { preference, setPreference } = useTheme()

  return (
    <div
      role="radiogroup"
      aria-label="Colour theme"
      className="inline-flex items-center gap-0.5 rounded-lg border border-line bg-surface-2 p-0.5"
    >
      {OPTIONS.map((option) => {
        const isActive = preference === option.value

        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={isActive}
            aria-label={option.label}
            title={option.label}
            onClick={() => setPreference(option.value)}
            className={`flex h-7 w-7 items-center justify-center rounded-md text-sm
                        transition-colors duration-150 ${
                          isActive
                            ? 'bg-brand-500 text-brand-fg'
                            : 'text-ink-500 hover:bg-ink-200 hover:text-ink-800'
                        }`}
          >
            {option.icon}
          </button>
        )
      })}
    </div>
  )
}
