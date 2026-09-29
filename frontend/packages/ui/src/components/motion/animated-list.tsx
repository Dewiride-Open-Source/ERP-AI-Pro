"use client";

import { AnimatePresence, LazyMotion, MotionConfig, domAnimation, m } from "motion/react";
import { useState, type ComponentProps, type ReactNode } from "react";

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
  | "initial"
  | "animate"
  | "exit"
  | "transition"
  | "className"
  | "children"
  | "layout"
  | "onAnimationStart"
  | "onAnimationComplete"
>) {
  const timing = useMotionTiming("normal", "enter");
  const [animating, setAnimating] = useState(false);

  // An item is clipped only while its height changes, so a focus ring drawn outside a control inside it stays visible once
  // the item has settled.
  return (
    <m.li
      {...props}
      data-slot="animated-list-item"
      data-animating={animating || undefined}
      className={cn(animating && "overflow-hidden", className)}
      initial={{ opacity: 0, height: 0 }}
      animate={{ opacity: 1, height: "auto" }}
      exit={{ opacity: 0, height: 0 }}
      transition={{ duration: timing.duration, ease: [...timing.ease] }}
      onAnimationStart={() => setAnimating(true)}
      onAnimationComplete={() => setAnimating(false)}
    >
      {children}
    </m.li>
  );
}
