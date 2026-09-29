"use client";

import { AnimatePresence, LazyMotion, MotionConfig, domAnimation, m } from "motion/react";
import type { ComponentProps, ReactNode } from "react";

import { cn } from "@dewiride/erp-ui/lib/utils";

import { useMotionTiming } from "./use-motion-timing";

export function AnimatedList({ className, children, ...props }: ComponentProps<"ul">) {
  return (
    <LazyMotion features={domAnimation} strict>
      <MotionConfig reducedMotion="user">
        <ul {...props} role="list" data-slot="animated-list" className={cn("flex flex-col", className)}>
          <AnimatePresence initial={false}>{children}</AnimatePresence>
        </ul>
      </MotionConfig>
    </LazyMotion>
  );
}

export function AnimatedListItem({
  className,
  children,
  ...props
}: {
  className?: string | undefined;
  children: ReactNode;
} & Omit<
  ComponentProps<typeof m.li>,
  "initial" | "animate" | "exit" | "transition" | "className" | "children" | "layout"
>) {
  const timing = useMotionTiming("normal", "enter");

  // An item is clipped only while its height changes, so a focus ring drawn outside a control inside it stays visible once
  // the item has settled.
  return (
    <m.li
      {...props}
      data-slot="animated-list-item"
      className={className}
      initial={{ opacity: 0, height: 0, overflow: "hidden" }}
      animate={{ opacity: 1, height: "auto", transitionEnd: { overflow: "visible" } }}
      exit={{ opacity: 0, height: 0, overflow: "hidden" }}
      transition={{ duration: timing.duration, ease: [...timing.ease] }}
    >
      {children}
    </m.li>
  );
}
