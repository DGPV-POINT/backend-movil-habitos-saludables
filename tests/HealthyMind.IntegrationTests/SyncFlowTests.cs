using System.Net;
using System.Net.Http.Json;
using HealthyMind.Application.Features.HabitRecords;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Features.Statistics;
using HealthyMind.Application.Features.Sync;
using HealthyMind.Domain.Habits;

namespace HealthyMind.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SyncFlowTests(ApiFactory factory)
{
    // Las fechas de registro se calculan en la zona del usuario (Ecuador por defecto).
    private static DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil")).DateTime);

    [Fact]
    public async Task Offline_changes_are_applied_and_reach_other_devices()
    {
        var (client, _) = await factory.RegisterAsync();
        var snapshot = await PullAsync(client, null);

        var habitId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var push = await PushAsync(client, new SyncPushRequest(
            "device-1",
            [new HabitChange(habitId, "Leer", HabitCategory.Lectura, 20, "páginas", null, TrackingMode.Manual, true, false, DateTimeOffset.UtcNow)],
            [new HabitRecordChange(recordId, habitId, Today.AddDays(-1), 25, "sin wifi", null, null, false, DateTimeOffset.UtcNow)]));

        Assert.Equal(SyncItemStatus.Applied, Assert.Single(push.Habits).Status);
        Assert.Equal(SyncItemStatus.Applied, Assert.Single(push.Records).Status);

        var changes = await PullAsync(client, snapshot.ServerTime);
        Assert.Contains(changes.Habits, item => item.Habit.Id == habitId);
        var record = Assert.Single(changes.Records, item => item.Record.Id == recordId).Record;
        Assert.True(record.Completed);
    }

    [Fact]
    public async Task Same_day_from_two_devices_is_merged_and_older_change_loses()
    {
        var (client, _) = await factory.RegisterAsync();
        var habit = await CreateHabitAsync(client, "Agua", goal: 8);

        var online = await client.PostAsJsonAsync(
            $"/api/habits/{habit.Id}/records",
            new CreateHabitRecordRequest(null, Today, 8, null, null, null),
            ApiFactory.Json);
        var serverRecord = (await online.Content.ReadFromJsonAsync<HabitRecordResponse>(ApiFactory.Json))!;

        // Otro teléfono registró el mismo día, sin conexión y antes.
        var push = await PushAsync(client, new SyncPushRequest(
            "device-2",
            null,
            [new HabitRecordChange(Guid.NewGuid(), habit.Id, Today, 2, null, null, null, false, DateTimeOffset.UtcNow.AddHours(-3))]));

        var result = Assert.Single(push.Records);
        Assert.Equal(SyncItemStatus.Stale, result.Status);
        Assert.Equal(serverRecord.Id, result.ServerId);
    }

    [Fact]
    public async Task One_bad_item_does_not_block_the_rest_of_the_batch()
    {
        var (client, _) = await factory.RegisterAsync();
        var habit = await CreateHabitAsync(client, "Caminar", goal: 6000);

        var push = await PushAsync(client, new SyncPushRequest(
            "device-1",
            null,
            [
                new HabitRecordChange(Guid.NewGuid(), Guid.NewGuid(), Today, 1, null, null, null, false, DateTimeOffset.UtcNow),
                new HabitRecordChange(Guid.NewGuid(), habit.Id, Today.AddDays(5), 1, null, null, null, false, DateTimeOffset.UtcNow),
                new HabitRecordChange(Guid.NewGuid(), habit.Id, Today, 7000, null, null, null, false, DateTimeOffset.UtcNow),
            ]));

        Assert.Equal(
            [SyncItemStatus.Rejected, SyncItemStatus.Rejected, SyncItemStatus.Applied],
            push.Records.Select(item => item.Status));
    }

    [Fact]
    public async Task Deleting_a_habit_propagates_tombstones_and_updates_statistics()
    {
        var (client, _) = await factory.RegisterAsync();
        var habit = await CreateHabitAsync(client, "Meditar", goal: 1);
        await client.PutAsJsonAsync($"/api/habits/{habit.Id}/records/{Today:yyyy-MM-dd}", new HabitProgressRequest(1, null, null, null), ApiFactory.Json);

        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/statistics/dashboard", ApiFactory.Json);
        Assert.Equal(100, dashboard!.TodayPercentage);

        var cursor = (await PullAsync(client, null)).ServerTime;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/habits/{habit.Id}")).StatusCode);

        var changes = await PullAsync(client, cursor);
        Assert.True(Assert.Single(changes.Habits, item => item.Habit.Id == habit.Id).IsDeleted);
        Assert.All(changes.Records.Where(item => item.Record.HabitId == habit.Id), item => Assert.True(item.IsDeleted));
    }

    private static async Task<HabitResponse> CreateHabitAsync(HttpClient client, string name, int goal)
    {
        var response = await client.PostAsJsonAsync(
            "/api/habits",
            new HabitRequest(null, name, HabitCategory.Salud, goal, "veces", null),
            ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HabitResponse>(ApiFactory.Json))!;
    }

    private static async Task<SyncPushResponse> PushAsync(HttpClient client, SyncPushRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/sync/push", request, ApiFactory.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncPushResponse>(ApiFactory.Json))!;
    }

    private static async Task<SyncPullResponse> PullAsync(HttpClient client, DateTimeOffset? since)
    {
        var url = since is null ? "/api/sync/pull" : $"/api/sync/pull?since={Uri.EscapeDataString(since.Value.ToString("O"))}";
        return (await client.GetFromJsonAsync<SyncPullResponse>(url, ApiFactory.Json))!;
    }
}
