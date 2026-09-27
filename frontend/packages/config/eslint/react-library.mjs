import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import prettier from "eslint-config-prettier/flat";

import { typeRoleRestrictions } from "./type-roles.mjs";

const webAppMessage = "A shared package never imports from apps/web: move the shared code into the package.";

export const reactLibraryConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  prettier,
  {
    rules: {
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports", fixStyle: "inline-type-imports" },
      ],
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }],
      "no-restricted-syntax": ["error", ...typeRoleRestrictions],
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            { regex: "^@dewiride/erp-web(?:/|$)", message: webAppMessage },
            { regex: String.raw`^(?:\./)?(?:\.\./)+(?:[^/]+/)*apps/web(?:/|$)`, message: webAppMessage },
          ],
        },
      ],
    },
  },
  globalIgnores(["dist/**"]),
]);
