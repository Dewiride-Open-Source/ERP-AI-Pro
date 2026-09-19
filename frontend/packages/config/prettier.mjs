/** @type {import("prettier").Config} */
const config = {
  printWidth: 110,
  singleQuote: false,
  semi: true,
  trailingComma: "all",
  plugins: ["prettier-plugin-tailwindcss"],
  tailwindStylesheet: "./packages/ui/src/styles/globals.css",
};

export default config;
