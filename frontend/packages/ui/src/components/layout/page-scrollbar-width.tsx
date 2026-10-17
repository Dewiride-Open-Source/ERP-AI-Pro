"use client";

import { useEffect } from "react";

import { pageScrollbarWidthProperty, scrollbarWidth, scrollLockedAttribute } from "./page-scrollbar";

// A modal Radix layer locks the page's scrolling through react-remove-scroll-bar, which hides the scrollbar and gives the
// body a right margin of innerWidth - clientWidth, a whole number of CSS pixels. The scrollbar is a whole number of device
// pixels, so at a zoom or display scale that does not divide it the margin misses a fraction of a pixel and the page moves
// by it as each layer opens and closes; base.css takes the margin from the width measured here, while nothing is locked.
export function PageScrollbarWidth() {
  useEffect(() => {
    const page = document.documentElement;
    const measure = () => {
      if (document.body.hasAttribute(scrollLockedAttribute)) return;
      const viewport = document.createElement("div");
      viewport.style.cssText =
        "position:fixed;inset-block-start:0;inset-inline-start:0;inline-size:100vw;block-size:0;visibility:hidden;pointer-events:none";
      document.body.append(viewport);
      const width = scrollbarWidth(
        viewport.getBoundingClientRect().width,
        page.getBoundingClientRect().width,
      );
      viewport.remove();
      page.style.setProperty(pageScrollbarWidthProperty, `${width}px`);
    };
    const observer = new ResizeObserver(measure);
    observer.observe(page);
    window.addEventListener("resize", measure);
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", measure);
      page.style.removeProperty(pageScrollbarWidthProperty);
    };
  }, []);

  return null;
}
