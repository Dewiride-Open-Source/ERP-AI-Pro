export const pageScrollbarWidthProperty = "--page-scrollbar-width";

export const scrollLockedAttribute = "data-scroll-locked";

export function scrollbarWidth(viewportWidth: number, pageWidth: number): number {
  return Math.max(0, viewportWidth - pageWidth);
}
