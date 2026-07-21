# Dignite Abp File Storing

This repository contains the extracted file storing and file explorer modules from `dignite-abp`.

The target layout is:

- `core/src/Dignite.Abp.FileStoring`: file upload infrastructure on top of ABP Blob Storing.
- `core/src/Dignite.Abp.FileStoring.Imaging`: optional upload-time image processing.
- `file-explorer/src/Dignite.FileExplorer.*`: DDD file explorer backend.
- `angular/projects/file-explorer`: Angular UI package.

`dignite-abp` is treated as a frozen source repository and is not modified by this extraction.
