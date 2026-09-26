import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@dewiride/erp-ui/components/ui/accordion";
import { AspectRatio } from "@dewiride/erp-ui/components/ui/aspect-ratio";
import {
  Avatar,
  AvatarBadge,
  AvatarFallback,
  AvatarGroup,
  AvatarGroupCount,
  AvatarImage,
} from "@dewiride/erp-ui/components/ui/avatar";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { Checkbox } from "@dewiride/erp-ui/components/ui/checkbox";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@dewiride/erp-ui/components/ui/collapsible";
import {
  Item,
  ItemActions,
  ItemContent,
  ItemDescription,
  ItemGroup,
  ItemMedia,
  ItemTitle,
} from "@dewiride/erp-ui/components/ui/item";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { ScrollArea, ScrollBar } from "@dewiride/erp-ui/components/ui/scroll-area";
import { Separator } from "@dewiride/erp-ui/components/ui/separator";
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";
import { BuildingIcon, ChevronsUpDownIcon, ImageIcon, ReceiptIcon } from "lucide-react";

import { Specimen, SpecimenGrid, SpecimenRow } from "../components/specimen";

const invoices = [
  { number: "INV-040", client: "Acme", amount: "₹59,000.00" },
  { number: "INV-041", client: "Globex", amount: "₹1,18,000.00" },
  { number: "INV-042", client: "Initech", amount: "₹23,600.00" },
] as const;

const columns = [
  "Invoice number",
  "Invoice date",
  "Client",
  "Place of supply",
  "Taxable value",
  "CGST",
  "SGST",
  "IGST",
  "Total",
  "Due date",
  "Status",
  "Created by",
] as const;

const quarters = [
  "Q1 2025–26",
  "Q2 2025–26",
  "Q3 2025–26",
  "Q4 2025–26",
  "Q1 2026–27",
  "Q2 2026–27",
] as const;

export function DataDisplayShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Card" description="Header with an action, content and footer; the small size below.">
        <Card>
          <CardHeader>
            <CardTitle>Receivables</CardTitle>
            <CardDescription>Outstanding across all clients.</CardDescription>
            <CardAction>
              <Button type="button" variant="outline" size="sm">
                View all
              </Button>
            </CardAction>
          </CardHeader>
          <CardContent>
            <p className="text-title">₹2,00,600.00</p>
          </CardContent>
          <CardFooter>
            <p className="text-caption text-muted-foreground">Updated a minute ago</p>
          </CardFooter>
        </Card>
        <Card size="sm">
          <CardHeader>
            <CardTitle>Overdue</CardTitle>
            <CardDescription>More than 30 days past the due date.</CardDescription>
          </CardHeader>
          <CardContent>
            <Badge variant="destructive">3 invoices</Badge>
          </CardContent>
        </Card>
      </Specimen>

      <Specimen title="Table" description="With a caption and a totals footer.">
        <Table data-testid="data-display-table">
          <TableCaption>Invoices issued in September 2026</TableCaption>
          <TableHeader>
            <TableRow>
              <TableHead>Invoice</TableHead>
              <TableHead>Client</TableHead>
              <TableHead className="text-right">Amount</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {invoices.map((invoice) => (
              <TableRow key={invoice.number}>
                <TableCell className="font-mono">{invoice.number}</TableCell>
                <TableCell className="whitespace-normal">{invoice.client}</TableCell>
                <TableCell className="text-right">{invoice.amount}</TableCell>
              </TableRow>
            ))}
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell colSpan={2}>Total</TableCell>
              <TableCell className="text-right">₹2,00,600.00</TableCell>
            </TableRow>
          </TableFooter>
        </Table>
      </Specimen>

      <Specimen
        title="Avatar"
        description="An image, initials when there is no image, sizes, a status badge and a group."
      >
        <SpecimenRow>
          <Avatar size="lg">
            <AvatarImage src="/icon.svg" alt="Dewiride" />
            <AvatarFallback>DW</AvatarFallback>
          </Avatar>
          <Avatar>
            <AvatarFallback>PS</AvatarFallback>
          </Avatar>
          <Avatar size="sm">
            <AvatarFallback>RK</AvatarFallback>
          </Avatar>
          <Avatar>
            <AvatarFallback>AM</AvatarFallback>
            <AvatarBadge className="bg-success">
              <span className="sr-only">Available</span>
            </AvatarBadge>
          </Avatar>
          <AvatarGroup>
            <Avatar>
              <AvatarFallback>PS</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback>RK</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback>AM</AvatarFallback>
            </Avatar>
            <AvatarGroupCount>+4</AvatarGroupCount>
          </AvatarGroup>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Item" description="Default, outline and muted items with media, text and actions.">
        <ItemGroup>
          <Item>
            <ItemMedia variant="icon">
              <BuildingIcon aria-hidden />
            </ItemMedia>
            <ItemContent>
              <ItemTitle>Acme Private Limited</ItemTitle>
              <ItemDescription>GSTIN 29AAACA1234A1Z5 · Bengaluru</ItemDescription>
            </ItemContent>
            <ItemActions>
              <Button type="button" variant="outline" size="sm">
                Open
              </Button>
            </ItemActions>
          </Item>
          <Item variant="outline">
            <ItemMedia variant="icon">
              <ReceiptIcon aria-hidden />
            </ItemMedia>
            <ItemContent>
              <ItemTitle>INV-2026-00042</ItemTitle>
              <ItemDescription>Due on 26 Oct 2026</ItemDescription>
            </ItemContent>
            <ItemActions>
              <Badge variant="secondary">Sent</Badge>
            </ItemActions>
          </Item>
          <Item variant="muted" asChild>
            <a href="#data-display">
              <ItemContent>
                <ItemTitle>Link item</ItemTitle>
                <ItemDescription>The whole item is one link.</ItemDescription>
              </ItemContent>
            </a>
          </Item>
        </ItemGroup>
      </Specimen>

      <Specimen title="Accordion" description="One panel open at a time; the last one is disabled.">
        <Accordion type="single" collapsible defaultValue="terms" data-testid="data-display-accordion">
          <AccordionItem value="terms">
            <AccordionTrigger>Payment terms</AccordionTrigger>
            <AccordionContent>Payment is due within 30 days of the invoice date.</AccordionContent>
          </AccordionItem>
          <AccordionItem value="bank">
            <AccordionTrigger>Bank details</AccordionTrigger>
            <AccordionContent>Pay by NEFT or RTGS to the account printed on the invoice.</AccordionContent>
          </AccordionItem>
          <AccordionItem value="late-fee" disabled>
            <AccordionTrigger>Late fee (not configured)</AccordionTrigger>
            <AccordionContent>No late fee applies.</AccordionContent>
          </AccordionItem>
        </Accordion>
      </Specimen>

      <Specimen
        title="Collapsible and separator"
        description="Content that expands in place; separators divide groups."
      >
        <Collapsible className="grid gap-2" data-testid="data-display-collapsible">
          <div className="flex items-center justify-between gap-3">
            <p className="text-body font-medium">Two more addresses</p>
            <CollapsibleTrigger asChild>
              <Button type="button" variant="ghost" size="icon-sm" aria-label="Show more addresses">
                <ChevronsUpDownIcon aria-hidden />
              </Button>
            </CollapsibleTrigger>
          </div>
          <p className="rounded-lg border px-3 py-2 text-body">Registered office, Bengaluru</p>
          <CollapsibleContent className="grid gap-2">
            <p className="rounded-lg border px-3 py-2 text-body">Branch office, Pune</p>
            <p className="rounded-lg border px-3 py-2 text-body">Warehouse, Chennai</p>
          </CollapsibleContent>
        </Collapsible>
        <Separator />
        <div className="flex h-5 items-center gap-3 text-body">
          <span>Draft</span>
          <Separator orientation="vertical" />
          <span>Sent</span>
          <Separator orientation="vertical" />
          <span>Paid</span>
        </div>
      </Specimen>

      <Specimen
        title="Scroll area"
        description="A vertical list and a horizontal row that scroll inside a fixed box."
      >
        <ScrollArea className="h-40 rounded-lg border" data-testid="data-display-scroll-vertical">
          <fieldset className="grid gap-2 p-3">
            <legend className="text-caption text-muted-foreground">Columns to show</legend>
            {columns.map((column, index) => {
              const id = `ks-scroll-column-${index}`;
              return (
                <div key={column} className="flex items-center gap-2">
                  <Checkbox id={id} defaultChecked={index < 5} />
                  <Label htmlFor={id}>{column}</Label>
                </div>
              );
            })}
          </fieldset>
        </ScrollArea>
        <ScrollArea className="w-full rounded-lg border" data-testid="data-display-scroll-horizontal">
          <div className="flex w-max gap-2 p-3 pb-4">
            {quarters.map((quarter) => (
              <Button key={quarter} type="button" variant="outline" size="sm">
                {quarter}
              </Button>
            ))}
          </div>
          <ScrollBar orientation="horizontal" />
        </ScrollArea>
      </Specimen>

      <Specimen title="Aspect ratio" description="Keeps media at a fixed shape at every width.">
        <AspectRatio ratio={16 / 9} data-testid="data-display-aspect-ratio">
          <div className="flex size-full flex-col items-center justify-center gap-2 rounded-lg border bg-muted text-caption text-muted-foreground">
            <ImageIcon className="size-6" aria-hidden />
            16 : 9
          </div>
        </AspectRatio>
      </Specimen>
    </SpecimenGrid>
  );
}
