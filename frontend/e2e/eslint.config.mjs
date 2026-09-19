import { nodeConfig } from "@dewiride/erp-config/eslint/node";
import { defineConfig } from "eslint/config";

export default defineConfig([
  ...nodeConfig,
  {
    files: ["**/*.ts"],
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
    rules: {
      "@typescript-eslint/no-floating-promises": "error",
      "@typescript-eslint/no-misused-promises": "error",
      "@typescript-eslint/await-thenable": "error",
    },
  },
]);
