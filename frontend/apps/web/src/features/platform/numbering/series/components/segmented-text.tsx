import { Fragment } from "react";

import type { TextSegment } from "./numbering-text";

export function SegmentedText({ segments }: { segments: readonly TextSegment[] }) {
  return segments.map((segment, index) => (
    <Fragment key={`${index}-${segment.text}`}>
      {segment.isNumber ? <span className="font-mono">{segment.text}</span> : segment.text}
    </Fragment>
  ));
}
