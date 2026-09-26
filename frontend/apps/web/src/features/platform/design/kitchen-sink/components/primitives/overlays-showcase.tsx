"use client";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogMedia,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@dewiride/erp-ui/components/ui/alert-dialog";
import { Avatar, AvatarFallback } from "@dewiride/erp-ui/components/ui/avatar";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { Field, FieldGroup, FieldLabel } from "@dewiride/erp-ui/components/ui/field";
import { HoverCard, HoverCardContent, HoverCardTrigger } from "@dewiride/erp-ui/components/ui/hover-card";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { Kbd } from "@dewiride/erp-ui/components/ui/kbd";
import {
  Popover,
  PopoverContent,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
} from "@dewiride/erp-ui/components/ui/popover";
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@dewiride/erp-ui/components/ui/sheet";
import { Tooltip, TooltipContent, TooltipTrigger } from "@dewiride/erp-ui/components/ui/tooltip";
import { SaveIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";

import { Specimen, SpecimenGrid, SpecimenRow } from "../specimen";

export function OverlaysShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Dialog" description="A modal task with a title, a description and a footer.">
        <SpecimenRow>
          <Dialog>
            <DialogTrigger asChild>
              <Button type="button" variant="outline">
                Edit contact
              </Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Edit contact</DialogTitle>
                <DialogDescription>Changes apply to invoices issued from now on.</DialogDescription>
              </DialogHeader>
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="ks-dialog-contact-name">Name</FieldLabel>
                  <Input id="ks-dialog-contact-name" defaultValue="Priya Sharma" />
                </Field>
                <Field>
                  <FieldLabel htmlFor="ks-dialog-contact-email">Email</FieldLabel>
                  <Input id="ks-dialog-contact-email" type="email" defaultValue="priya@example.com" />
                </Field>
              </FieldGroup>
              <DialogFooter>
                <DialogClose asChild>
                  <Button type="button" variant="outline">
                    Cancel
                  </Button>
                </DialogClose>
                <DialogClose asChild>
                  <Button type="button" onClick={() => toast.success("Contact saved")}>
                    Save
                  </Button>
                </DialogClose>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </SpecimenRow>
      </Specimen>

      <Specimen
        title="Alert dialog"
        description="A confirmation that cannot be dismissed by clicking outside."
      >
        <SpecimenRow>
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button type="button" variant="destructive">
                <Trash2Icon data-icon="inline-start" aria-hidden />
                Delete draft
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogMedia>
                  <Trash2Icon aria-hidden />
                </AlertDialogMedia>
                <AlertDialogTitle>Delete this draft invoice?</AlertDialogTitle>
                <AlertDialogDescription>
                  The draft has no number yet, so deleting it leaves no gap in the invoice series.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Keep it</AlertDialogCancel>
                <AlertDialogAction variant="destructive" onClick={() => toast.success("Draft deleted")}>
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Sheet" description="A panel that slides in from the right or from the bottom.">
        <SpecimenRow>
          <Sheet>
            <SheetTrigger asChild>
              <Button type="button" variant="outline">
                Open filters
              </Button>
            </SheetTrigger>
            <SheetContent side="right">
              <SheetHeader>
                <SheetTitle>Filters</SheetTitle>
                <SheetDescription>Narrow the invoice list.</SheetDescription>
              </SheetHeader>
              <div className="px-4">
                <Field>
                  <FieldLabel htmlFor="ks-sheet-client">Client</FieldLabel>
                  <Input id="ks-sheet-client" placeholder="Any client" />
                </Field>
              </div>
              <SheetFooter>
                <SheetClose asChild>
                  <Button type="button">Apply</Button>
                </SheetClose>
              </SheetFooter>
            </SheetContent>
          </Sheet>
          <Sheet>
            <SheetTrigger asChild>
              <Button type="button" variant="outline">
                Show totals
              </Button>
            </SheetTrigger>
            <SheetContent side="bottom">
              <SheetHeader>
                <SheetTitle>Totals</SheetTitle>
                <SheetDescription>
                  Taxable value ₹1,00,000.00 · GST ₹18,000.00 · Total ₹1,18,000.00
                </SheetDescription>
              </SheetHeader>
            </SheetContent>
          </Sheet>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Popover" description="Extra detail anchored to its trigger.">
        <SpecimenRow>
          <Popover>
            <PopoverTrigger asChild>
              <Button type="button" variant="outline">
                Tax breakdown
              </Button>
            </PopoverTrigger>
            <PopoverContent aria-labelledby="ks-popover-title" aria-describedby="ks-popover-description">
              <PopoverHeader>
                <PopoverTitle id="ks-popover-title">Tax breakdown</PopoverTitle>
                <PopoverDescription id="ks-popover-description">
                  Intra-state supply, so GST splits into CGST and SGST.
                </PopoverDescription>
              </PopoverHeader>
              <dl className="grid grid-cols-2 gap-1 text-body">
                <dt className="text-muted-foreground">CGST 9 %</dt>
                <dd className="text-right">₹9,000.00</dd>
                <dt className="text-muted-foreground">SGST 9 %</dt>
                <dd className="text-right">₹9,000.00</dd>
              </dl>
            </PopoverContent>
          </Popover>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Hover card" description="A preview on hover or keyboard focus, for pointer users.">
        <SpecimenRow>
          <HoverCard>
            <HoverCardTrigger asChild>
              <Button type="button" variant="link">
                Acme Private Limited
              </Button>
            </HoverCardTrigger>
            <HoverCardContent data-testid="overlay-hover-card">
              <div className="flex gap-3">
                <Avatar>
                  <AvatarFallback>AP</AvatarFallback>
                </Avatar>
                <div className="grid gap-1">
                  <p className="font-medium">Acme Private Limited</p>
                  <p className="text-caption text-muted-foreground">Client since April 2024 · Bengaluru</p>
                </div>
              </div>
            </HoverCardContent>
          </HoverCard>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Tooltip" description="A short label on hover or keyboard focus.">
        <SpecimenRow>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button type="button" variant="outline" size="icon" aria-label="Save">
                <SaveIcon aria-hidden />
              </Button>
            </TooltipTrigger>
            <TooltipContent data-testid="overlay-tooltip">
              Save <Kbd>Ctrl S</Kbd>
            </TooltipContent>
          </Tooltip>
        </SpecimenRow>
      </Specimen>
    </SpecimenGrid>
  );
}
