# Resume Build Validation

Windows CI must confirm:

- `StartupForm` compiles
- `JobResumeService` compiles
- New-job flow still opens `MainForm`
- Resume flow loads `gis_job.json`
- Existing valid output is not rerun
- Failed or missing output is rerun with versioned output
- Portable single-file EXE is still produced
- No-admin checks still pass

Final status must remain `passed_with_warnings` until tested manually with a real GIS dataset and installed QGIS or ArcGIS Pro.
