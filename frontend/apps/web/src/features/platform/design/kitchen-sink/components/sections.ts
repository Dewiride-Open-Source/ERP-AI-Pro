export const kitchenSinkGroups = [
  { id: "tokens", title: "Tokens" },
  { id: "primitives", title: "Primitives" },
] as const;

export const kitchenSinkSections = [
  {
    id: "colours",
    group: "tokens",
    title: "Colours",
    description:
      "Every colour token in both themes, grouped by role, with the pairings text and fills are allowed to use.",
  },
  {
    id: "typography",
    group: "tokens",
    title: "Typography",
    description: "Font families and weights, the five type roles and tabular numerals for amounts.",
  },
  {
    id: "spacing",
    group: "tokens",
    title: "Spacing",
    description:
      "The numeric spacing steps and the named layout tokens for gutters, sections, the header and the page.",
  },
  {
    id: "radius",
    group: "tokens",
    title: "Radius",
    description: "Corner radii derived from one base radius.",
  },
  {
    id: "elevation",
    group: "tokens",
    title: "Elevation",
    description: "Shadows tinted by theme so raised surfaces read in light and dark.",
  },
  {
    id: "layers",
    group: "tokens",
    title: "Layers",
    description: "The stacking order of sticky, overlay and notification layers.",
  },
  {
    id: "focus",
    group: "tokens",
    title: "Focus",
    description:
      "The keyboard focus indicator of every control the design system does not generate: an outline in the ring colour, drawn outside the element.",
  },
  {
    id: "motion",
    group: "tokens",
    title: "Motion",
    description:
      "Durations and easing curves. Every duration collapses to 0.01ms when the device asks for reduced motion.",
  },
  {
    id: "actions",
    group: "primitives",
    title: "Actions",
    description: "Buttons, button groups, toggles and keyboard shortcuts in every variant, size and state.",
  },
  {
    id: "inputs",
    group: "primitives",
    title: "Inputs",
    description:
      "Text entry, selects and field layouts, including disabled, read-only, invalid and loading states.",
  },
  {
    id: "selection",
    group: "primitives",
    title: "Selection",
    description: "Checkboxes, radio groups, switches and sliders.",
  },
  {
    id: "feedback",
    group: "primitives",
    title: "Feedback",
    description: "Alerts, badges, progress, skeletons, spinners, empty states and toasts.",
  },
  {
    id: "overlays",
    group: "primitives",
    title: "Overlays",
    description: "Dialogs, confirmations, sheets, popovers, hover cards and tooltips.",
  },
  {
    id: "menus",
    group: "primitives",
    title: "Menus",
    description: "Dropdown, context and menu bar menus with shortcuts, checkable items and submenus.",
  },
  {
    id: "navigation",
    group: "primitives",
    title: "Navigation",
    description: "Tabs, breadcrumbs, pagination and the navigation menu.",
  },
  {
    id: "data-display",
    group: "primitives",
    title: "Data display",
    description:
      "Cards, tables, avatars, items, accordions, collapsibles, separators, scroll areas and aspect ratios.",
  },
  {
    id: "composites",
    group: "primitives",
    title: "Composites",
    description:
      "Components the design system builds from primitives: the theme switch and the file drop zone.",
  },
  {
    id: "excluded",
    group: "primitives",
    title: "Excluded primitives",
    description:
      "Registry items and recipes that are not installed, why, and the roadmap item that decides whether they arrive.",
  },
] as const;

export type KitchenSinkSectionId = (typeof kitchenSinkSections)[number]["id"];
