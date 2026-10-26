import js from "@eslint/js";
import tseslint from "typescript-eslint";
import globals from "globals";

export default tseslint.config(
  { ignores: ["dist/**", "node_modules/**"] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ["**/*.{ts,mjs}"],
    languageOptions: { globals: { ...globals.browser, ...globals.worker, ...globals.node } },
    rules: {
      "no-console": "error",
      curly: ["error", "all"],
      "max-lines": ["error", { max: 300, skipBlankLines: true, skipComments: true }],
      "no-restricted-syntax": ["error",
        { selector: "TSUnknownKeyword", message: "Use an explicit type." },
        { selector: "TSAsExpression > TSAsExpression", message: "Avoid nested assertions." },
        { selector: "MemberExpression[property.name='localStorage']", message: "Do not use localStorage." },
        { selector: "Identifier[name='localStorage']", message: "Do not use localStorage." },
        { selector: "Literal[value=/[0-9](d|s|l)?v[wh]/]", message: "Use relative container sizing." }
      ]
    }
  },
  {
    files: ["**/*.ts"],
    languageOptions: {
      parserOptions: {
        projectService: { allowDefaultProject: ["tests/*.test.ts"] },
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unsafe-type-assertion": "error",
    },
  },
);
