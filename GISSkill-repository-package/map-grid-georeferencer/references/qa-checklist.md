# QA checklist

1. Preserve the original file and checksum it.
2. Inspect the full page before cropping; separate page edge, map frame, legend, inset and decorations.
3. Confirm coordinate labels by numeric sequence and direction.
4. Confirm datum, projection, zone, hemisphere, units and axis order. Treat an incomplete label such as “UTM Zone 47” as unresolved.
5. Use the simplest adequate transform. Do not increase model flexibility merely to reduce residuals.
6. Require distributed control and independent checkpoints where available.
7. Inspect residual patterns, output extent, orientation, mirroring and overlay.
8. Validate extracted geometry, polygon closure, self-intersection and contamination from legend/text.
9. Convert KML geometry to WGS 84 longitude-latitude order.
10. Report confidence and prohibited uses. Do not claim survey, cadastral or legal accuracy without survey evidence.
