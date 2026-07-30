# Resume Build Validation

## Validated build

```text
Workflow: Build GISPlan Portable
Run number: 14
Run ID: 30533231023
Head commit tested: 7cfe66d2f350e7fa292876de6e5ca80d4bd72f15
Conclusion: success
Validation date: 2026-07-30
Review status: passed_with_warnings
```

## Checks

- `StartupForm` compiled: passed
- `JobResumeService` compiled: passed
- Core smoke tests: passed
- Portable single-file EXE produced: passed
- No-admin static acceptance: passed
- Artifact uploaded: passed

## Artifact

```text
Name: GISPlan-win-x64
Artifact ID: 8755569785
Size: 67,445,241 bytes
Digest: sha256:8b3ee0c7c2f38554be6dadce5d7f94b1685c2f36c89a38527208f26de8835f59
Expires: 2026-10-28
```

Runtime behavior still requires manual testing on the user's Windows computer with a real GIS dataset and installed QGIS, ArcGIS Pro, or GDAL. AI must not set `approved`.
