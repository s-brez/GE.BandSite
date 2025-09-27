using GE.BandSite.Database.Media;
using GE.BandSite.Server.Features.Media.Admin;
using GE.BandSite.Server.Features.Media.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GE.BandSite.Server.Controllers;

[ApiController]
[Route("api/admin/media")]
[Authorize]
public sealed class AdminMediaController : ControllerBase
{
    private readonly IMediaAdminService _mediaAdminService;

    public AdminMediaController(IMediaAdminService mediaAdminService)
    {
        _mediaAdminService = mediaAdminService;
    }

    [HttpPost("uploads")]
    public async Task<IActionResult> CreateUploadAsync([FromBody] CreateUploadRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MediaUploadKind>(request.UploadKind, ignoreCase: true, out var kind))
        {
            return BadRequest($"Unsupported upload kind '{request.UploadKind}'.");
        }

        try
        {
            var upload = await _mediaAdminService.CreateUploadAsync(
                new MediaUploadRequest(kind, request.FileName, request.ContentType, request.ContentLength),
                cancellationToken).ConfigureAwait(false);

            return Ok(new CreateUploadResponse(upload.UploadUrl, upload.ObjectKey, upload.ExpiresAt, upload.ContentType));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("assets/photo")]
    public async Task<IActionResult> CreatePhotoAsync([FromBody] CreatePhotoAssetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var asset = await _mediaAdminService.CreatePhotoAssetAsync(
                new CreatePhotoAssetParameters(
                    request.Title,
                    request.RawObjectKey,
                    request.ContentType,
                    request.Description,
                    request.IsFeatured,
                    request.ShowOnHome,
                    request.IsPublished,
                    request.DisplayOrder),
                cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(nameof(CreatePhotoAsync), new { id = asset.Id }, new MediaAssetResponse(asset.Id, asset.AssetType, asset.StoragePath, asset.PlaybackPath, asset.PosterPath));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("assets/video")]
    public async Task<IActionResult> CreateVideoAsync([FromBody] CreateVideoAssetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var asset = await _mediaAdminService.CreateVideoAssetAsync(
                new CreateVideoAssetParameters(
                    request.Title,
                    request.RawVideoKey,
                    request.VideoContentType,
                    request.Description,
                    request.RawPosterKey,
                    request.PosterContentType,
                    request.IsFeatured,
                    request.ShowOnHome,
                    request.IsPublished,
                    request.DisplayOrder),
                cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(nameof(CreateVideoAsync), new { id = asset.Id }, new MediaAssetResponse(asset.Id, asset.AssetType, asset.StoragePath, asset.PlaybackPath, asset.PosterPath));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CreateUploadRequest
    {
        [JsonRequired]
        [JsonProperty("upload_kind")]
        public string UploadKind { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("content_length")]
        public long ContentLength { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CreateUploadResponse
    {
        public CreateUploadResponse(string uploadUrl, string objectKey, DateTimeOffset expiresAt, string contentType)
        {
            UploadUrl = uploadUrl;
            ObjectKey = objectKey;
            ExpiresAt = expiresAt;
            ContentType = contentType;
        }

        [JsonProperty("upload_url")]
        public string UploadUrl { get; }

        [JsonProperty("object_key")]
        public string ObjectKey { get; }

        [JsonProperty("expires_at")]
        public DateTimeOffset ExpiresAt { get; }

        [JsonProperty("content_type")]
        public string ContentType { get; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CreatePhotoAssetRequest
    {
        [JsonRequired]
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("raw_object_key")]
        public string RawObjectKey { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("is_featured")]
        public bool IsFeatured { get; set; }

        [JsonProperty("show_on_home")]
        public bool ShowOnHome { get; set; }

        [JsonProperty("is_published")]
        public bool IsPublished { get; set; } = true;

        [JsonProperty("display_order")]
        public int DisplayOrder { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CreateVideoAssetRequest
    {
        [JsonRequired]
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("raw_video_key")]
        public string RawVideoKey { get; set; } = string.Empty;

        [JsonRequired]
        [JsonProperty("video_content_type")]
        public string VideoContentType { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("raw_poster_key")]
        public string? RawPosterKey { get; set; }

        [JsonProperty("poster_content_type")]
        public string? PosterContentType { get; set; }

        [JsonProperty("is_featured")]
        public bool IsFeatured { get; set; }

        [JsonProperty("show_on_home")]
        public bool ShowOnHome { get; set; }

        [JsonProperty("is_published")]
        public bool IsPublished { get; set; } = false;

        [JsonProperty("display_order")]
        public int DisplayOrder { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MediaAssetResponse
    {
        public MediaAssetResponse(Guid id, MediaAssetType assetType, string storagePath, string? playbackPath, string? posterPath)
        {
            Id = id;
            AssetType = assetType.ToString();
            StoragePath = storagePath;
            PlaybackPath = playbackPath;
            PosterPath = posterPath;
        }

        [JsonProperty("id")]
        public Guid Id { get; }

        [JsonProperty("asset_type")]
        public string AssetType { get; }

        [JsonProperty("storage_path")]
        public string StoragePath { get; }

        [JsonProperty("playback_path")]
        public string? PlaybackPath { get; }

        [JsonProperty("poster_path")]
        public string? PosterPath { get; }
    }
}
