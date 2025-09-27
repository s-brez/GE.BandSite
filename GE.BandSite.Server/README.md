# GE.BandSite.Server

## Recent Changes

- Stage 7 media upload interface completed: admin Razor page now issues presigned S3 uploads via new `MediaStorageService`/`MediaAdminService` abstractions and pushes processed output back through the updated media pipeline.
- Request logging now populates `RequestPathSanitized` and masks long tracking tokens (UUID-like fragments) before sending entries to Serilog. Static helper `SanitizePath` is exercised in unit tests.
- Admin/authentication startup pipeline registers Serilog request logging conditionally via `RequestLoggingConfiguration` and maps Razor Pages, controllers, and static files in the corrected middleware order.
- JWT validation middleware ensures cookies are refreshed with case-insensitive user discovery backed by database changes (see GE.BandSite.Database/README.md).

## Media Storage Configuration

- Configure `MediaStorage` in `appsettings`/environment variables to point at the production S3 bucket. Supported keys:
  - `BucketName`: required; without it presigned uploads are rejected.
  - `RawUploadPrefix`, `PhotoPrefix`, `VideoSourcePrefix`, `VideoPlaybackPrefix`, `PosterPrefix`: control raw/published key layouts.
  - `PresignedExpiryMinutes`, `MaxPhotoBytes`, `MaxVideoBytes`, `MaxPosterBytes`: constrain upload behaviour.
- Admin uploads use `/api/admin/media/uploads` to obtain presigned PUT URLs and finalize assets via `/api/admin/media/assets/photo|video`. Ensure the target bucket has CORS rules permitting authenticated PUT from the admin origin.
