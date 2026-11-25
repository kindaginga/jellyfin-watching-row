using System;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Jellyfin.Plugin.WatchingRow.Tests;

public class RequestFilterTests
{
    private const string User = "6f1c2a3b4d5e46f7a8b9c0d1e2f3a4b5";

    private static HttpRequest Request(string method, string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [Theory]
    [InlineData("/UserItems/Resume", "?userId=" + User + "&Limit=12&MediaTypes=Video&EnableTotalRecordCount=false")]
    [InlineData("/UserItems/Resume", "?userId=" + User)]
    [InlineData("/Users/" + User + "/Items/Resume", "?mediaTypes=Video")]
    public void The_home_screen_video_row_is_answered(string path, string query)
    {
        Assert.Equal(Guid.Parse(User), ResumeRowMiddleware.MergeableUser(Request("GET", path, query)));
    }

    [Theory]
    [InlineData("GET", "/UserItems/Resume", "?userId=" + User + "&mediaTypes=Audio")]
    [InlineData("GET", "/UserItems/Resume", "?userId=" + User + "&mediaTypes=Book")]
    [InlineData("GET", "/UserItems/Resume", "?userId=" + User + "&parentId=1234")]
    [InlineData("GET", "/UserItems/Resume", "?userId=" + User + "&startIndex=12")]
    [InlineData("GET", "/UserItems/Resume", "?userId=" + User + "&searchTerm=avatar")]
    [InlineData("POST", "/UserItems/Resume", "?userId=" + User)]
    [InlineData("GET", "/UserItems/Resume", "")]
    [InlineData("GET", "/Shows/NextUp", "?userId=" + User)]
    [InlineData("GET", "/UserItems/Resume/extra", "?userId=" + User)]
    public void Every_other_request_is_left_to_Jellyfin(string method, string path, string query)
    {
        Assert.Null(ResumeRowMiddleware.MergeableUser(Request(method, path, query)));
    }
}

public class HomeNextUpTests
{
    private static HttpRequest Request(string method, string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [Fact]
    public void The_home_screen_next_up_row_is_recognized()
    {
        Assert.True(ResumeRowMiddleware.IsHomeNextUp(Request("GET", "/Shows/NextUp",
            "?userId=6f1c2a3b4d5e46f7a8b9c0d1e2f3a4b5&limit=24&imageTypeLimit=1&enableTotalRecordCount=false&nextUpDateCutoff=2025-11-24&enableResumable=false&enableRewatching=false")));
    }

    [Theory]
    [InlineData("GET", "/Shows/NextUp", "?SeriesId=abc&UserId=def")]
    [InlineData("GET", "/Shows/NextUp", "?ParentId=abc&nextUpDateCutoff=2025-11-24")]
    [InlineData("GET", "/Shows/NextUp", "?Limit=24&EnableTotalRecordCount=false&SortBy=DatePlayed")]
    [InlineData("GET", "/Shows/NextUp", "?nextUpDateCutoff=2025-11-24&SortBy=DatePlayed")]
    [InlineData("GET", "/Shows/NextUp", "?nextUpDateCutoff=2025-11-24&startIndex=24")]
    [InlineData("GET", "/Shows/NextUp", "?seriesId=abc&nextUpDateCutoff=2025-11-24")]
    [InlineData("POST", "/Shows/NextUp", "?nextUpDateCutoff=2025-11-24")]
    [InlineData("GET", "/UserItems/Resume", "?nextUpDateCutoff=2025-11-24")]
    public void Series_pages_libraries_the_next_up_page_and_other_requests_are_left_alone(string method, string path, string query)
    {
        Assert.False(ResumeRowMiddleware.IsHomeNextUp(Request(method, path, query)));
    }
}
