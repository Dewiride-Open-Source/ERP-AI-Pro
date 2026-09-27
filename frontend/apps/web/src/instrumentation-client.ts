// Imported by name: this file is bundled into every page, and the z namespace would bring all of Zod with it.
import { config } from "zod";

// The content security policy allows no eval, and Zod probes for it with new Function the first time an object schema is
// built so it can compile a faster parser; with the probe refused the browser reports a policy violation on every form page.
config({ jitless: true });
