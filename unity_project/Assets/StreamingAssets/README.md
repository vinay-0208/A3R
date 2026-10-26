# A³R StreamingAssets — Required Files

This folder must contain the MediaPipe Face Landmarker model file before building the Android APK.

## Required File

| File | Size | Source |
|------|------|--------|
| `face_landmarker.task` | ~2.8 MB | MediaPipe official model |

## Download Instructions

Download `face_landmarker.task` from the official MediaPipe Models page:

```
https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task
```

Place it directly in this folder:
```
unity_project/Assets/StreamingAssets/face_landmarker.task
```

## Why This File Is Not in Git

The `.task` file is a binary model blob (~2.8 MB). It is gitignored to keep the repository lean.
It must be downloaded once during project setup.

## Verification

After placing the file, confirm it is present:
- In Unity Editor: Project window → Assets/StreamingAssets/face_landmarker.task should appear
- File size should be approximately 2.7–3.0 MB
- Do NOT rename the file — `FaceLandmarkBridge.cs` expects exactly: `face_landmarker.task`
