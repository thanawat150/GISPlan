# Configuration schema

Use JSON. Never fill unknown coordinates or CRS values by guessing.

```json
{
  "source_crs": "EPSG:32647",
  "axis_calibration": {
    "x_ticks": [{"pixel_x": 421, "easting": 667500}],
    "y_ticks": [{"pixel_y": 535, "northing": 1515000}]
  },
  "boundary_detection": {
    "s_min": 120,
    "v_min": 80,
    "close_kernel": 5,
    "close_iterations": 2,
    "min_contour_area_px": 10000,
    "simplify_px": 2
  },
  "feature_name": "ขอบเขตพื้นที่",
  "confidence": "moderate"
}
```

For full control points, replace `axis_calibration` with:

```json
"gcps": [
  {"pixel_x": 100, "pixel_y": 100, "map_x": 667500, "map_y": 1515000}
]
```

Use at least 3 non-collinear GCPs; prefer 6 or more distributed points. The script fits an affine transform and reports residuals.
