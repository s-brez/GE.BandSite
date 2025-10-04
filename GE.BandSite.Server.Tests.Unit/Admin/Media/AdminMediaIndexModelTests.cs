using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Pages.Admin.Media;
using GE.BandSite.Testing.Core;
using Microsoft.AspNetCore.Mvc;
using NodaTime;

namespace GE.BandSite.Server.Tests.Admin.Media;

[TestFixture]
[NonParallelizable]
public class AdminMediaIndexModelTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private IndexModel _pageModel = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _pageModel = new IndexModel(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
            _dbContext = null!;
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
            _postgres = null!;
        }
    }

    [Test]
    public async Task OnPostUpdateAsync_PersistsAllPlacementFlags()
    {
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = "Existing Asset",
            StoragePath = "videos/originals/existing.mp4",
            AssetType = MediaAssetType.Video,
            ProcessingState = MediaProcessingState.Pending,
            IsPublished = false,
            ShowOnHome = false,
            IsFeatured = false,
            DisplayOrder = 0,
            CreatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        await _dbContext.MediaAssets.AddAsync(asset);
        await _dbContext.SaveChangesAsync();

        IActionResult result = await _pageModel.OnPostUpdateAsync(asset.Id, true, true, true);

        var updated = await _dbContext.MediaAssets.FindAsync(asset.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<RedirectToPageResult>());
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.IsPublished, Is.True);
            Assert.That(updated.ShowOnHome, Is.True);
            Assert.That(updated.IsFeatured, Is.True);
        });
    }
}
