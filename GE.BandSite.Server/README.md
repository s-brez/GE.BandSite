# GE.BandSite.Server

## Recent Changes

- Stage 9 media transcode pipeline: hosted worker now converts uploaded `.mov` sources to H.264/AAC MP4 using FFmpeg/ffprobe, captures duration + resolution metadata, and rewrites playback keys so the public site and hero fallback always serve `.mp4` assets. The runbook documents how to requeue legacy `.mov` uploads for regeneration.
- Existing S3 media is auto-imported on startup: when a bucket is configured the bootstrapper scans photo/video prefixes, creates missing `MediaAsset` rows, and marks legacy `.mov` sources for background transcode. Duplicate delivery-only rows are cleaned up automatically so the admin UI only surfaces the canonical source-backed asset for each playback file.
- Stage 7 media upload interface completed: admin Razor page now issues presigned S3 uploads via new `MediaStorageService`/`MediaAdminService` abstractions and pushes processed output back through the updated media pipeline.
- Request logging now populates `RequestPathSanitized` and masks long tracking tokens (UUID-like fragments) before sending entries to Serilog. Static helper `SanitizePath` is exercised in unit tests.
- Admin/authentication startup pipeline registers Serilog request logging conditionally via `RequestLoggingConfiguration` and maps Razor Pages, controllers, and static files in the corrected middleware order.
- JWT validation middleware ensures cookies are refreshed with case-insensitive user discovery backed by database changes (see GE.BandSite.Database/README.md).

## Authentication

- An explicit admin login Razor Page now lives at `/Login` and relies on the existing `ILoginService`. Any unauthenticated admin request (including Razor pages under `/Admin` and API calls under `/api/admin/*`) will be redirected there by the JWT middleware.

## Media Processing & Delivery

- Originals always remain under the configured source prefixes (`MediaStorage:PhotoSourcePrefix`, `MediaStorage:VideoSourcePrefix`). Processed assets are emitted alongside the original file name with a suffix:
  - Photos: `{original-name}_web.jpg`
  - Videos: `{original-name}_mp4.mp4`
- To keep behaviour consistent with the hosted worker, set the following configuration values (defaults shown):

```json
"MediaStorage": {
  "PhotoSourcePrefix": "images/originals",
  "PhotoPrefix": "images",
  "VideoSourcePrefix": "videos/originals",
  "VideoPlaybackPrefix": "videos"
},
"MediaProcessing": {
  "PhotoOptimizationEnabled": true,
  "PhotoMaxWidth": 2048,
  "PhotoMaxHeight": 2048,
  "PhotoJpegQuality": 85
}
```

- The admin media page now displays only delivery-ready assets; originals remain hidden from selection. When a processed asset is not yet available the table shows “Awaiting optimisation” so content editors know why an item cannot be published.

## Media Storage Configuration

- Configure `MediaStorage` in `appsettings`/environment variables to point at the production S3 bucket. Supported keys:
  - `BucketName`: required; without it presigned uploads are rejected.
  - `RawUploadPrefix`, `PhotoPrefix`, `VideoSourcePrefix`, `VideoPlaybackPrefix`, `PosterPrefix`: control raw/published key layouts.
  - `PresignedExpiryMinutes`, `MaxPhotoBytes`, `MaxVideoBytes`, `MaxPosterBytes`: constrain upload behaviour.
- Admin uploads use `/api/admin/media/uploads` to obtain presigned PUT URLs and finalize assets via `/api/admin/media/assets/photo|video`. Ensure the target bucket has CORS rules permitting authenticated PUT from the admin origin.
