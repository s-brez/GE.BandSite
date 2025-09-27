# GE.BandSite.Database

## Recent Changes

- `Extensions.GeBandSiteDbContext.GetUserByEmailAsync` now uses `EF.Functions.ILike` so case-insensitive email lookups run entirely on PostgreSQL without client-side evaluation. JWT/login integration tests depend on this behaviour.
