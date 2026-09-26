import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";

type RoadmapItem = { id: string; title: string };

const authenticatedShell: RoadmapItem = {
  id: "authentication-authenticated-application-shell",
  title: "Authenticated application shell",
};
const formsKit: RoadmapItem = {
  id: "web-foundation-forms-and-validation-kit",
  title: "Forms and validation kit",
};
const dashboards: RoadmapItem = {
  id: "reporting-web-dashboards-and-reports",
  title: "Web: dashboards and reports",
};
const reconciliation: RoadmapItem = {
  id: "finance-banking-web-reconciliation-and-remittances",
  title: "Web: reconciliation and remittances",
};
const assistant: RoadmapItem = { id: "ai-platform-assistant-ux", title: "Assistant UX" };
const themePackage: RoadmapItem = {
  id: "web-foundation-design-tokens-and-theme-package",
  title: "Design tokens and theme package",
};

const excludedPrimitives: readonly {
  primitives: string;
  needs: string;
  decidedIn: RoadmapItem | undefined;
}[] = [
  {
    primitives: "sidebar",
    needs:
      "Its generated use-mobile hook sets state inside an effect, which the React hooks lint rules refuse.",
    decidedIn: authenticatedShell,
  },
  {
    primitives: "combobox",
    needs: "@base-ui/react; the design system builds on Radix only.",
    decidedIn: formsKit,
  },
  {
    primitives: "calendar, date-picker",
    needs: "react-day-picker and date-fns, which are not approved packages.",
    decidedIn: formsKit,
  },
  { primitives: "command", needs: "cmdk, which is not an approved package.", decidedIn: authenticatedShell },
  {
    primitives: "drawer",
    needs: "vaul, which is not an approved package; the shell uses sheet for mobile navigation.",
    decidedIn: authenticatedShell,
  },
  { primitives: "chart", needs: "recharts, which is not an approved package.", decidedIn: dashboards },
  {
    primitives: "carousel",
    needs: "embla-carousel-react, which is not an approved package; no screen needs a carousel.",
    decidedIn: undefined,
  },
  {
    primitives: "input-otp",
    needs: "input-otp, which is not an approved package; Microsoft Entra ID handles sign-in codes.",
    decidedIn: undefined,
  },
  {
    primitives: "resizable",
    needs: "react-resizable-panels, which is not an approved package.",
    decidedIn: reconciliation,
  },
  {
    primitives: "direction",
    needs: "Right-to-left layout only; the interface is English only.",
    decidedIn: undefined,
  },
  {
    primitives: "attachment, bubble, marker, message",
    needs: "The chat set for the AI assistant; attachment also clashes in name with the attachments module.",
    decidedIn: assistant,
  },
  {
    primitives: "message-scroller, questionnaire",
    needs: "@shadcn/react, which is not an approved package.",
    decidedIn: assistant,
  },
  {
    primitives: "form",
    needs: "An empty registry item; forms are built from field and a form library.",
    decidedIn: formsKit,
  },
  {
    primitives: "toast",
    needs: "Exists for Base UI only; toasts come from sonner.",
    decidedIn: themePackage,
  },
];

export function ExcludedPrimitives() {
  return (
    <div className="min-w-0 rounded-xl border bg-card p-4">
      <Table data-testid="excluded-primitives">
        <TableCaption>Registry primitives that are not installed in the design system.</TableCaption>
        <TableHeader>
          <TableRow>
            <TableHead>Primitive</TableHead>
            <TableHead>Why it is not installed</TableHead>
            <TableHead>Decided in</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {excludedPrimitives.map((entry) => (
            <TableRow key={entry.primitives}>
              <TableCell className="align-top font-mono whitespace-normal">{entry.primitives}</TableCell>
              <TableCell className="align-top whitespace-normal">{entry.needs}</TableCell>
              <TableCell className="align-top whitespace-normal">
                {entry.decidedIn ? (
                  <>
                    {entry.decidedIn.title}
                    <code className="block font-mono text-caption break-all text-muted-foreground">
                      {entry.decidedIn.id}
                    </code>
                  </>
                ) : (
                  <span className="text-muted-foreground">Not planned</span>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
