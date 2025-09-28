# Hero Video Transcoding


## WebM
```powershell
# Desktop (~1280p) VP9 + Opus
ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=1280:-2" -c:v libvpx-vp9 -b:v 0 -crf 36 -speed 1 -tile-columns 2 -frame-parallel 1 -row-mt 1 -c:a libopus -b:a 80k -movflags +faststart STB-PromoMain-Horizontal_1280p.webm

# Mobile (~720p) VP9 + Opus
ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=-1:720" -c:v libvpx-vp9 -b:v 0 -crf 36 -speed 1 -tile-columns 2 -frame-parallel 1 -row-mt 1 -c:a libopus -b:a 64k -movflags +faststart STB-PromoMain-Horizontal_720p.webm
```


## MP4
```powershell
# Desktop fallback ~1280p (keeps 16:9 automatically)
ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=1280:-2" -c:v libx264 -preset slow -crf 28 -profile:v high -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart STB-PromoMain-Horizontal_1280p.mp4

# Mobile fallback ~720p
ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=-2:720" -c:v libx264 -preset slow -crf 30 -profile:v main -pix_fmt yuv420p -c:a aac -b:a 80k -movflags +faststart STB-PromoMain-Horizontal_720p.mp4

```



ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=1280:-2" -c:v libvpx-vp9 -b:v 0 -crf 36 -speed 1 -tile-columns 2 -frame-parallel 1 -row-mt 1 -c:a libopus -b:a 80k -movflags +faststart STB-PromoMain-Horizontal_1280p.webm; ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=-1:720" -c:v libvpx-vp9 -b:v 0 -crf 36 -speed 1 -tile-columns 2 -frame-parallel 1 -row-mt 1 -c:a libopus -b:a 64k -movflags +faststart STB-PromoMain-Horizontal_720p.webm; ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=1280:-2" -c:v libx264 -preset slow -crf 28 -profile:v high -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart STB-PromoMain-Horizontal_1280p.mp4; ffmpeg -i STB-PromoMain-Horizontal_trimmed.mp4 -vf "scale=-2:720" -c:v libx264 -preset slow -crf 30 -profile:v main -pix_fmt yuv420p -c:a aac -b:a 80k -movflags +faststart STB-PromoMain-Horizontal_720p.mp4