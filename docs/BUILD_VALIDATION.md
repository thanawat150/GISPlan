# Build Validation

## Latest validated build

```text
Workflow: Build GISPlan Portable
Run number: 7
Run ID: 30532732533
Head commit tested: f744d205af1220f2b7dd2f2e071a996c2cd5b802
Windows runner result: success
Validation date: 2026-07-30
Review status: passed_with_warnings
```

## Acceptance result

- Core project compiled: passed
- Desktop WinForms project compiled: passed
- Smoke tests: passed
- Self-contained single-file `GISPlan.exe`: produced
- Application manifest requests `asInvoker`: passed
- No-admin static checks: passed
- Build artifact upload: passed

## Artifact

```text
Name: GISPlan-win-x64
Artifact ID: 8755375266
Artifact size: 67,441,071 bytes
Artifact digest: sha256:76760db8e63cdcd9dcf06f1064364879dec21497754b0eecac4b258bac947d3b
Expires: 2026-10-28
```

ZIP contents:

```text
GISPlan-win-x64/GISPlan.exe
GISPlan-win-x64/README_TH.md
no_admin_test.json
```

The executable was created successfully, but human acceptance on the user's actual office computer is still required for QGIS/ArcGIS/GDAL detection, real GIS processing, antivirus reputation, and organization policy constraints. AI must not set `approved`.
