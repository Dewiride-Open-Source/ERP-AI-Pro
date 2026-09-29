"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";
import { type ReactNode } from "react";

import { themeStorageKey } from "./stored-theme";

type ThemeProviderProps = {
  children: ReactNode;
  nonce?: string;
};

export function ThemeProvider({ children, nonce }: ThemeProviderProps) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      storageKey={themeStorageKey}
      disableTransitionOnChange
      {...(nonce === undefined ? {} : { nonce })}
    >
      {children}
    </NextThemesProvider>
  );
}
