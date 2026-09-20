import { Button } from "@dewiride/erp-ui/components/ui/button";
import { ArrowRightIcon } from "lucide-react";

import { apiBasePath } from "@/shared/api/base-path";

export function SignInButton() {
  return (
    <Button asChild size="lg" className="group h-11 w-full justify-between text-base">
      <a href={`${apiBasePath}/auth/login?returnUrl=%2F`} data-testid="sign-in-microsoft">
        <span className="inline-flex items-center gap-3">
          <MicrosoftLogo />
          Continue with Microsoft
        </span>
        <ArrowRightIcon className="size-4 transition-transform group-hover:translate-x-0.5" aria-hidden />
      </a>
    </Button>
  );
}

function MicrosoftLogo() {
  return (
    <svg viewBox="0 0 21 21" className="size-4" aria-hidden>
      <rect x="1" y="1" width="9" height="9" fill="#f25022" />
      <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
      <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
    </svg>
  );
}
