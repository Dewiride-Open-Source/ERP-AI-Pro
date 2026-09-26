import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  ButtonGroup,
  ButtonGroupSeparator,
  ButtonGroupText,
} from "@dewiride/erp-ui/components/ui/button-group";
import { Kbd, KbdGroup } from "@dewiride/erp-ui/components/ui/kbd";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { Toggle } from "@dewiride/erp-ui/components/ui/toggle";
import { ToggleGroup, ToggleGroupItem } from "@dewiride/erp-ui/components/ui/toggle-group";
import {
  AlignCenterIcon,
  AlignLeftIcon,
  AlignRightIcon,
  BoldIcon,
  ChevronDownIcon,
  DownloadIcon,
  ItalicIcon,
  MinusIcon,
  PlusIcon,
  UnderlineIcon,
} from "lucide-react";

import { Specimen, SpecimenGrid, SpecimenRow } from "../specimen";

const buttonVariantNames = ["default", "outline", "secondary", "ghost", "destructive", "link"] as const;
const buttonSizes = ["xs", "sm", "default", "lg"] as const;
const iconSizes = ["icon-xs", "icon-sm", "icon", "icon-lg"] as const;

export function ActionsShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Button" description="Every variant in every size, then icon buttons." wide>
        {buttonVariantNames.map((variant) => (
          <SpecimenRow key={variant} label={variant}>
            {buttonSizes.map((size) => (
              <Button key={size} type="button" variant={variant} size={size}>
                {labelFor(variant)} · {size}
              </Button>
            ))}
          </SpecimenRow>
        ))}
        <SpecimenRow label="icon sizes">
          {iconSizes.map((size) => (
            <Button key={size} type="button" variant="outline" size={size} aria-label={`Add (${size})`}>
              <PlusIcon aria-hidden />
            </Button>
          ))}
        </SpecimenRow>
      </Specimen>

      <Specimen title="Button states" description="With an icon, disabled, invalid and loading.">
        <SpecimenRow>
          <Button type="button">
            <DownloadIcon data-icon="inline-start" aria-hidden />
            Export
          </Button>
          <Button type="button" disabled>
            Disabled
          </Button>
          <Button type="button" variant="outline" disabled>
            Disabled outline
          </Button>
          <Button type="button" variant="outline" aria-invalid="true">
            Invalid
          </Button>
          <Button type="button" disabled aria-busy="true">
            <Spinner data-icon="inline-start" aria-hidden="true" />
            Saving
          </Button>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Button group" description="Related actions joined into one control.">
        <SpecimenRow>
          <ButtonGroup aria-label="Invoice actions">
            <Button type="button" variant="outline">
              Send
            </Button>
            <Button type="button" variant="outline">
              Duplicate
            </Button>
            <Button type="button" variant="outline" aria-label="More invoice actions">
              <ChevronDownIcon aria-hidden />
            </Button>
          </ButtonGroup>
          <ButtonGroup aria-label="Quantity">
            <ButtonGroupText>Qty</ButtonGroupText>
            <Button type="button" variant="outline" size="icon" aria-label="Decrease quantity">
              <MinusIcon aria-hidden />
            </Button>
            <ButtonGroupSeparator />
            <Button type="button" variant="outline" size="icon" aria-label="Increase quantity">
              <PlusIcon aria-hidden />
            </Button>
          </ButtonGroup>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Toggle" description="Pressed, not pressed, outline and disabled.">
        <SpecimenRow>
          <Toggle aria-label="Bold" defaultPressed>
            <BoldIcon aria-hidden />
          </Toggle>
          <Toggle aria-label="Italic">
            <ItalicIcon aria-hidden />
          </Toggle>
          <Toggle variant="outline" aria-label="Underline">
            <UnderlineIcon aria-hidden />
            Underline
          </Toggle>
          <Toggle aria-label="Bold (disabled)" disabled>
            <BoldIcon aria-hidden />
          </Toggle>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Toggle group" description="Single choice, multiple choice and a disabled item.">
        <SpecimenRow label="single">
          <ToggleGroup
            type="single"
            defaultValue="left"
            variant="outline"
            spacing={0}
            aria-label="Text alignment"
          >
            <ToggleGroupItem value="left" aria-label="Align left">
              <AlignLeftIcon aria-hidden />
            </ToggleGroupItem>
            <ToggleGroupItem value="center" aria-label="Align centre">
              <AlignCenterIcon aria-hidden />
            </ToggleGroupItem>
            <ToggleGroupItem value="right" aria-label="Align right" disabled>
              <AlignRightIcon aria-hidden />
            </ToggleGroupItem>
          </ToggleGroup>
        </SpecimenRow>
        <SpecimenRow label="multiple">
          <ToggleGroup type="multiple" defaultValue={["bold"]} aria-label="Text style">
            <ToggleGroupItem value="bold" aria-label="Bold">
              <BoldIcon aria-hidden />
            </ToggleGroupItem>
            <ToggleGroupItem value="italic" aria-label="Italic">
              <ItalicIcon aria-hidden />
            </ToggleGroupItem>
            <ToggleGroupItem value="underline" aria-label="Underline">
              <UnderlineIcon aria-hidden />
            </ToggleGroupItem>
          </ToggleGroup>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Keyboard shortcut" description="Keys shown in help text, menus and tooltips.">
        <SpecimenRow>
          <KbdGroup>
            <Kbd>Ctrl</Kbd>
            <span aria-hidden>+</span>
            <Kbd>K</Kbd>
          </KbdGroup>
          <Kbd>Esc</Kbd>
          <Kbd>Enter</Kbd>
        </SpecimenRow>
      </Specimen>
    </SpecimenGrid>
  );
}

function labelFor(variant: (typeof buttonVariantNames)[number]) {
  return variant.charAt(0).toUpperCase() + variant.slice(1);
}
