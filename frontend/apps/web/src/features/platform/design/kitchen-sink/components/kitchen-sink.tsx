import type { ReactNode } from "react";

import { ExcludedPrimitives } from "./excluded-primitives";
import { KitchenSinkIndex } from "./kitchen-sink-index";
import { KitchenSinkSection } from "./kitchen-sink-section";
import { ActionsShowcase } from "./primitives/actions-showcase";
import { CompositesShowcase } from "./primitives/composites-showcase";
import { DataDisplayShowcase } from "./primitives/data-display-showcase";
import { FeedbackShowcase } from "./primitives/feedback-showcase";
import { InputsShowcase } from "./primitives/inputs-showcase";
import { MenusShowcase } from "./primitives/menus-showcase";
import { NavigationShowcase } from "./primitives/navigation-showcase";
import { OverlaysShowcase } from "./primitives/overlays-showcase";
import { SelectionShowcase } from "./primitives/selection-showcase";
import { kitchenSinkSections, type KitchenSinkSectionId } from "./sections";
import { ColourSwatches } from "./tokens/colour-swatches";
import { ElevationScale } from "./tokens/elevation-scale";
import { FocusScale } from "./tokens/focus-scale";
import { LayerScale } from "./tokens/layer-scale";
import { MotionDemo } from "./tokens/motion-demo";
import { MotionScale } from "./tokens/motion-scale";
import { RadiusScale } from "./tokens/radius-scale";
import { SpacingScale } from "./tokens/spacing-scale";
import { TypographyScale } from "./tokens/typography-scale";

export function KitchenSink() {
  const content: Record<KitchenSinkSectionId, ReactNode> = {
    colours: <ColourSwatches />,
    typography: <TypographyScale />,
    spacing: <SpacingScale />,
    radius: <RadiusScale />,
    elevation: <ElevationScale />,
    layers: <LayerScale />,
    focus: <FocusScale />,
    motion: (
      <div className="grid min-w-0 grid-cols-1 gap-4 lg:grid-cols-2">
        <MotionScale />
        <MotionDemo />
      </div>
    ),
    actions: <ActionsShowcase />,
    inputs: <InputsShowcase />,
    selection: <SelectionShowcase />,
    feedback: <FeedbackShowcase />,
    overlays: <OverlaysShowcase />,
    menus: <MenusShowcase />,
    navigation: <NavigationShowcase />,
    "data-display": <DataDisplayShowcase />,
    composites: <CompositesShowcase />,
    excluded: <ExcludedPrimitives />,
  };

  return (
    <div data-testid="kitchen-sink" className="grid min-w-0 animate-fade-up grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Design system</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          Every token and every installed primitive of the ERP design system, in each state it supports.
          Switch the theme from the header to check both themes.
        </p>
      </header>

      <KitchenSinkIndex />

      {kitchenSinkSections.map((section) => (
        <KitchenSinkSection
          key={section.id}
          id={section.id}
          title={section.title}
          description={section.description}
        >
          {content[section.id]}
        </KitchenSinkSection>
      ))}
    </div>
  );
}
