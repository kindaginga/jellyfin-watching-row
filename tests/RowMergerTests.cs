using System;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.WatchingRow.Tests;

public class RowMergerTests
{
    private static readonly Guid Avatar = Guid.NewGuid();
    private static readonly Guid Ncis = Guid.NewGuid();
    private static readonly Guid Eureka = Guid.NewGuid();

    private static RowEntry Entry(Guid? series, int day) => new(Guid.NewGuid(), series, new DateTime(2026, 11, day));

    [Fact]
    public void Shows_with_a_half_watched_episode_and_shows_with_a_next_episode_share_one_row_newest_first()
    {
        var avatar = Entry(Avatar, 24);
        var ncis = Entry(Ncis, 20);
        var eureka = Entry(Eureka, 22);
        var movie = Entry(null, 23);
        var row = RowMerger.Merge(new[] { avatar, ncis, movie }, new[] { eureka });
        Assert.Equal(new[] { avatar, movie, eureka, ncis }, row);
    }

    [Fact]
    public void A_series_appears_once_and_its_half_watched_episode_wins_over_its_next_episode()
    {
        var halfWatched = Entry(Avatar, 24);
        var next = Entry(Avatar, 24);
        var row = RowMerger.Merge(new[] { halfWatched }, new[] { next });
        Assert.Equal(new[] { halfWatched }, row);
    }

    [Fact]
    public void An_item_in_both_lists_appears_once()
    {
        var episode = Entry(Avatar, 24);
        var row = RowMerger.Merge(new[] { episode }, new[] { episode });
        Assert.Single(row);
    }

    [Fact]
    public void Movies_are_never_merged_with_each_other()
    {
        var first = Entry(null, 20);
        var second = Entry(null, 21);
        var row = RowMerger.Merge(new[] { first, second }, Array.Empty<RowEntry>());
        Assert.Equal(new[] { second, first }, row);
    }

    [Fact]
    public void Items_without_a_watch_date_go_last_in_their_input_order()
    {
        var undated1 = new RowEntry(Guid.NewGuid(), Ncis, null);
        var undated2 = new RowEntry(Guid.NewGuid(), Eureka, null);
        var dated = Entry(Avatar, 1);
        var row = RowMerger.Merge(Array.Empty<RowEntry>(), new[] { undated1, undated2, dated });
        Assert.Equal(new[] { dated, undated1, undated2 }, row.ToArray());
    }
}
