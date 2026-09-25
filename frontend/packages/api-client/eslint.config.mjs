import { globalIgnores } from "eslint/config";

import { nodeConfig } from "@dewiride/erp-config/eslint/node";

export default [...nodeConfig, globalIgnores(["dist/**", "src/generated/**"])];
