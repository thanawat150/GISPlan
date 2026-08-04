---
name: map-grid-georeferencer
description: Georeference scanned maps, images, or PDF map pages that lack embedded coordinates but contain map-frame grid ticks, coordinate labels, sheet references, or validated control evidence; extract a highlighted boundary or linework and deliver KML plus optional GeoPackage/GeoJSON and QA evidence. Use for map registration, border-grid calibration, semi-automatic red-boundary extraction, PDF-to-KML workflows, and GIS conversion where CRS, datum, zone, hemisphere, units, axis order, and control quality must be validated before processing.
---

# Map Grid Georeferencer

Operate as a semi-automatic geospatial review workflow. Preserve original files. Never invent coordinates, CRS metadata, EPSG codes, GCPs, residuals, or accuracy.

## Required workflow

1. **Clarify the objective and intended use.** Determine whether the output is for visualization, reconnaissance, operational GIS, engineering, cadastral, or legal use. Treat engineering, cadastral, and legal use as blocked unless authoritative control and independent validation are available.
2. **Inspect the complete document.** Identify page boundary, actual map frame, neatline, title, legend, coordinate ticks, grid labels, CRS/datum notes, zone, hemisphere, scale, north reference, inset maps, and decorations. Do not crop margins before inspection.
3. **Classify the spatial-reference evidence.** Separate declared CRS from confirmed CRS. Validate coordinate ranges, units, axis order, area of use, datum, projection, zone, hemisphere, and geographic context. Treat labels such as `WGS 84 / UTM Zone 47` as incomplete until hemisphere and coordinate consistency are verified.
4. **Rank control evidence.** Prefer published coordinate-grid intersections and frame ticks over feature matching, OCR, or visual estimation. Validate OCR values by sequence and spacing. Do not use unreadable labels.
5. **Choose the automation mode.**
   - Use automatic mode only when labels, CRS, grid geometry, and independent checks are high confidence.
   - Use semi-automatic mode by default: propose ticks/GCPs and require confirmation for uncertain values.
   - Use manual mode when labels are readable but detection is unreliable.
6. **Choose the simplest adequate transform.** Use axis calibration or affine transformation for rectified scans with orthogonal grids. Use projective transformation only for photographed planar maps with verified control. Do not use higher-order transforms merely to force visual alignment.
7. **Extract the target feature.** Mask legend, text, north arrow, scale bar, stamps, logos, and page decorations. For a highlighted red boundary, use HSV-based segmentation, connected-component filtering, contour extraction, and topology validation. Do not assume every red pixel is the target.
8. **Convert to GIS coordinates.** Transform pixel geometry using the validated registration. Preserve source CRS in GeoPackage/GeoJSON. Transform to WGS 84 longitude-latitude before writing KML.
9. **Validate.** Check residuals, control distribution, extent, orientation, mirroring, raster/vector overlay, polygon validity, closure, self-intersections, area plausibility, and independent checkpoints. Reopen every output.
10. **Report status.** Use `COMPLETE`, `COMPLETE WITH WARNINGS`, `BLOCKED`, or `FAILED VALIDATION`. Never report `COMPLETE` without output evidence.

## Deterministic script

Use `scripts/georeference_extract.py` after the operator confirms the CRS and tick/GCP values.

```bash
python scripts/georeference_extract.py \
  --input input-map.png \
  --config config.json \
  --output-dir output
```

The script supports PNG/JPEG/TIFF and PDF pages, calibrates from border-axis ticks or affine GCPs, extracts the largest validated red contour, and writes:

- `boundary.kml` in WGS 84 longitude-latitude
- `boundary.gpkg` in the confirmed source CRS
- `boundary.geojson` in WGS 84
- `red_mask.png`
- `qa_report.json`

Read `references/config-schema.md` before preparing configuration. Apply `references/qa-checklist.md` before acceptance.

## Blocking conditions

Stop and request evidence when any of the following applies:

- CRS, datum, units, axis order, zone, or hemisphere remains unresolved.
- Fewer than two validated ticks exist for either axis, or fewer than three non-collinear full GCPs exist.
- Coordinate sequences conflict or OCR confidence is insufficient.
- Map frame cannot be distinguished from page border.
- Extracted boundary is incomplete or contaminated by legend/annotation.
- Registration residuals exceed project tolerance or independent checks fail.
- KML coordinate ranges are invalid.

## Output report

Include:

- Objective and intended use
- Active evidence and missing information
- Confirmed or candidate CRS with confidence
- Control points/ticks and source
- Transformation method
- Residuals and independent checks
- Geometry/topology results
- Output list and CRS for each file
- Limitations, prohibited uses, acceptance status, and confidence
