#!/usr/bin/env python3
"""Georeference a rectified map from border grid ticks and extract a colored boundary to KML.

This tool is intentionally semi-automatic. It never guesses CRS or coordinates.
The operator must provide validated axis ticks or full GCPs in JSON.
"""
from __future__ import annotations
import argparse, json, math, sys
from pathlib import Path
from typing import Any
import cv2
import numpy as np
from PIL import Image
import fitz
from pyproj import CRS, Transformer
from shapely.geometry import Polygon, mapping
from shapely.validation import make_valid
import geopandas as gpd


def load_image(path: Path, page: int = 1, dpi: int = 300) -> np.ndarray:
    if path.suffix.lower() == '.pdf':
        doc = fitz.open(path)
        if page < 1 or page > len(doc):
            raise ValueError(f'PDF page {page} outside 1..{len(doc)}')
        pix = doc[page-1].get_pixmap(matrix=fitz.Matrix(dpi/72, dpi/72), alpha=False)
        arr = np.frombuffer(pix.samples, dtype=np.uint8).reshape(pix.height, pix.width, pix.n)
        return cv2.cvtColor(arr[:, :, :3], cv2.COLOR_RGB2BGR)
    arr = cv2.imread(str(path), cv2.IMREAD_COLOR)
    if arr is None:
        raise ValueError(f'Cannot read image: {path}')
    return arr


def fit_axis(ticks: list[dict[str, float]], pixel_key: str, value_key: str) -> tuple[float, float, list[float]]:
    if len(ticks) < 2:
        raise ValueError(f'At least 2 {value_key} ticks are required')
    p = np.array([float(t[pixel_key]) for t in ticks])
    v = np.array([float(t[value_key]) for t in ticks])
    a, b = np.polyfit(p, v, 1)
    residuals = (a*p + b - v).tolist()
    return float(a), float(b), residuals


def affine_from_config(cfg: dict[str, Any]) -> tuple[np.ndarray, dict[str, Any]]:
    if 'axis_calibration' in cfg:
        cal = cfg['axis_calibration']
        ax, bx, rx = fit_axis(cal['x_ticks'], 'pixel_x', 'easting')
        ay, by, ry = fit_axis(cal['y_ticks'], 'pixel_y', 'northing')
        M = np.array([[ax, 0.0, bx], [0.0, ay, by]], dtype=float)
        qa = {'mode':'axis_calibration','x_residuals_map_units':rx,'y_residuals_map_units':ry,
              'x_rmse':float(np.sqrt(np.mean(np.square(rx)))),
              'y_rmse':float(np.sqrt(np.mean(np.square(ry))))}
        return M, qa
    gcps = cfg.get('gcps', [])
    if len(gcps) < 3:
        raise ValueError('Provide axis_calibration or at least 3 non-collinear GCPs')
    src = np.array([[g['pixel_x'], g['pixel_y']] for g in gcps], dtype=np.float64)
    dst = np.array([[g['map_x'], g['map_y']] for g in gcps], dtype=np.float64)
    M, inliers = cv2.estimateAffine2D(src, dst, method=cv2.RANSAC, ransacReprojThreshold=float(cfg.get('ransac_threshold', 5.0)))
    if M is None:
        raise ValueError('Affine estimation failed')
    pred = np.c_[src, np.ones(len(src))] @ M.T
    residual = np.linalg.norm(pred-dst, axis=1)
    qa = {'mode':'gcps','residuals_map_units':residual.tolist(),'rmse':float(np.sqrt(np.mean(residual**2))),
          'max_residual':float(residual.max()),'inliers':inliers.ravel().astype(int).tolist() if inliers is not None else None}
    return M, qa


def extract_red_polygon(img: np.ndarray, cfg: dict[str, Any]) -> tuple[Polygon, np.ndarray]:
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    smin = int(cfg.get('s_min', 120)); vmin = int(cfg.get('v_min', 80))
    low1 = np.array([0, smin, vmin]); high1 = np.array([12, 255, 255])
    low2 = np.array([168, smin, vmin]); high2 = np.array([179, 255, 255])
    mask = cv2.inRange(hsv, low1, high1) | cv2.inRange(hsv, low2, high2)
    k = int(cfg.get('close_kernel', 5)); kernel = np.ones((k,k), np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=int(cfg.get('close_iterations', 2)))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    min_area = float(cfg.get('min_contour_area_px', 10000))
    contours = [c for c in contours if cv2.contourArea(c) >= min_area]
    if not contours:
        raise ValueError('No red boundary contour passed the minimum area threshold')
    c = max(contours, key=cv2.contourArea)
    eps = float(cfg.get('simplify_px', 2.0))
    c = cv2.approxPolyDP(c, eps, True)
    poly = Polygon(c[:,0,:])
    poly = make_valid(poly)
    if poly.geom_type == 'MultiPolygon':
        poly = max(poly.geoms, key=lambda g: g.area)
    if not poly.is_valid or poly.area <= 0:
        raise ValueError('Extracted pixel polygon is invalid')
    return poly, mask


def transform_polygon(poly: Polygon, M: np.ndarray) -> Polygon:
    xy = np.asarray(poly.exterior.coords)
    out = np.c_[xy, np.ones(len(xy))] @ M.T
    result = make_valid(Polygon(out))
    if result.geom_type == 'MultiPolygon':
        result = max(result.geoms, key=lambda g: g.area)
    return result


def write_kml(poly: Polygon, src_crs: CRS, out: Path, name: str, description: str) -> None:
    tr = Transformer.from_crs(src_crs, CRS.from_epsg(4326), always_xy=True)
    coords = [tr.transform(float(x), float(y)) for x,y in poly.exterior.coords]
    coord_text = ' '.join(f'{lon:.10f},{lat:.10f},0' for lon,lat in coords)
    esc = lambda s: s.replace('&','&amp;').replace('<','&lt;').replace('>','&gt;')
    text = f'''<?xml version="1.0" encoding="UTF-8"?>\n<kml xmlns="http://www.opengis.net/kml/2.2"><Document><Placemark><name>{esc(name)}</name><description>{esc(description)}</description><Style><LineStyle><color>ff0000ff</color><width>3</width></LineStyle><PolyStyle><color>330000ff</color></PolyStyle></Style><Polygon><outerBoundaryIs><LinearRing><coordinates>{coord_text}</coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark></Document></kml>'''
    out.write_text(text, encoding='utf-8')


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--input', required=True)
    ap.add_argument('--config', required=True)
    ap.add_argument('--output-dir', required=True)
    ap.add_argument('--page', type=int, default=1)
    args = ap.parse_args()
    inp=Path(args.input); cfg=json.loads(Path(args.config).read_text(encoding='utf-8')); out=Path(args.output_dir); out.mkdir(parents=True, exist_ok=True)
    src_crs = CRS.from_user_input(cfg['source_crs'])
    img=load_image(inp,args.page,int(cfg.get('pdf_dpi',300)))
    M, regqa=affine_from_config(cfg)
    poly_px, mask=extract_red_polygon(img,cfg.get('boundary_detection',{}))
    poly_map=transform_polygon(poly_px,M)
    name=cfg.get('feature_name','Extracted boundary')
    desc=cfg.get('description','Semi-automatic extraction from map grid ticks; validate independently before authoritative use.')
    write_kml(poly_map,src_crs,out/'boundary.kml',name,desc)
    gdf=gpd.GeoDataFrame([{'name':name,'source_file':inp.name,'confidence':cfg.get('confidence','moderate')}],geometry=[poly_map],crs=src_crs)
    gdf.to_file(out/'boundary.gpkg',layer='boundary',driver='GPKG')
    gdf.to_crs(4326).to_file(out/'boundary.geojson',driver='GeoJSON')
    cv2.imwrite(str(out/'red_mask.png'),mask)
    qa={'input':str(inp),'image_width':int(img.shape[1]),'image_height':int(img.shape[0]),'source_crs':src_crs.to_string(),
        'registration':regqa,'pixel_polygon_area':float(poly_px.area),'map_polygon_area':float(poly_map.area),
        'map_bounds':list(map(float,poly_map.bounds)),'geometry_valid':bool(poly_map.is_valid),
        'warning':'Output is not survey/legal grade without authoritative control and independent checkpoints.'}
    (out/'qa_report.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'status':'COMPLETE WITH WARNINGS','outputs':[str(out/'boundary.kml'),str(out/'boundary.gpkg'),str(out/'boundary.geojson'),str(out/'qa_report.json')]},ensure_ascii=False))
    return 0

if __name__=='__main__':
    try: raise SystemExit(main())
    except Exception as e:
        print(json.dumps({'status':'FAILED VALIDATION','error':str(e)},ensure_ascii=False),file=sys.stderr)
        raise SystemExit(2)
