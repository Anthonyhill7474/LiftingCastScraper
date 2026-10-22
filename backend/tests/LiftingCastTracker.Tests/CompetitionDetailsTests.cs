using System.Text.Json;
using LiftingCastTracker.Services.Models;
using LiftingCastTracker.Services.Parsing;

namespace LiftingCastTracker.Tests;

public sealed class CompetitionDetailsTests
{
    [Fact]
    public void JoinsAttemptDocumentsToLiftersAndResolvesObservedDivisionReferences()
    {
        var documents = new[]
        {
            CreateDocument(
                "division:1",
                """{"name":"Open","weightClasses":{"class:1":{"_id":"class:1","name":"83 kg"}}}"""),
            CreateDocument(
                "lifter:1",
                """
                {
                  "name": "Lifter One",
                  "divisions": [
                    {
                      "divisionId": "division:1",
                      "declaredAwardsWeightClassId": "class:1",
                      "rawOrEquipped": "Raw"
                    }
                  ],
                  "flight": "A",
                  "session": 1,
                  "lot": 12,
                  "bodyWeight": 82.5,
                  "platformId": "platform:1"
                }
                """),
            CreateDocument(
                "attempt:deadlift-1",
                """{"lifterId":"lifter:1","liftName":"Deadlift","attemptNumber":"1","weight":180,"result":"good"}"""),
            CreateDocument(
                "attempt:squat-2",
                """{"lifterId":"lifter:1","liftName":"Squat","attemptNumber":"2","weight":120,"result":"pending"}"""),
            CreateDocument(
                "attempt:unknown-lifter",
                """{"lifterId":"missing-lifter","liftName":"Squat","attemptNumber":"1","weight":100}"""),
            CreateDocument(
                "platform:1",
                """{"name":"Platform A","clockState":"running","clockTimerLength":60,"currentAttemptId":"attempt:squat-2"}""")
        };
        var competition = new Competition
        {
            Id = "meet-1",
            Name = "Local Meet",
            Date = "22/10/2026",
            DateFormat = "DD/MM/YYYY"
        };
        var syncedAt = DateTimeOffset.Parse("2026-10-22T00:00:00Z");

        var details = CompetitionDetailsMapper.Create(competition, documents, syncedAt);

        var lifter = Assert.Single(details.Lifters);
        var division = Assert.Single(lifter.Divisions);
        Assert.Equal("Lifter One", lifter.Name);
        Assert.Equal("A", lifter.Flight);
        Assert.Equal(1, lifter.Session?.GetInt32());
        Assert.Equal(12, lifter.Lot?.GetInt32());
        Assert.Equal(82.5, lifter.BodyWeight?.GetDouble());
        Assert.Equal("platform:1", lifter.PlatformId);
        Assert.Equal("Open", division.Name);
        Assert.Equal("83 kg", division.WeightClassName);
        Assert.Equal("Raw", division.RawOrEquipped);
        Assert.Equal(new[] { "attempt:deadlift-1", "attempt:squat-2" }, lifter.Attempts.Select(attempt => attempt.DocumentId));
        Assert.Equal("120", lifter.Attempts[1].Weight?.GetRawText());
        Assert.Equal("good", lifter.Attempts[0].Result?.GetString());
        var platform = Assert.Single(details.Platforms);
        Assert.Equal("running", platform.ClockState);
        Assert.Equal("attempt:squat-2", platform.CurrentAttemptId);
        Assert.Equal(syncedAt, details.LastSyncedAtUtc);
    }

    [Fact]
    public void PreservesMissingAndExplicitNullOptionalValues()
    {
        var competition = new Competition { Id = "meet-1", Name = "Upcoming" };
        var documents = new[]
        {
            CreateDocument(
                "lifter:missing",
                """{"name":"No division data","flight":null}"""),
            CreateDocument(
                "attempt:incomplete",
                """{"lifterId":"lifter:missing","liftName":"Bench"}""")
        };

        var details = CompetitionDetailsMapper.Create(competition, documents, null);

        var lifter = Assert.Single(details.Lifters);
        Assert.Null(details.LastSyncedAtUtc);
        Assert.Null(lifter.Flight);
        Assert.Null(lifter.Session);
        Assert.Null(lifter.Lot);
        Assert.Null(lifter.BodyWeight);
        Assert.Null(lifter.PlatformId);
        Assert.Empty(lifter.Divisions);
        var attempt = Assert.Single(lifter.Attempts);
        Assert.Null(attempt.AttemptNumber);
        Assert.Null(attempt.Weight);
        Assert.Null(attempt.Result);
    }

    [Fact]
    public void SortsStringAttemptNumbersNumericallyForObservedLiftNames()
    {
        var documents = new[]
        {
            CreateDocument("lifter:1", """{"name":"Three-lift lifter","flight":"A"}"""),
            CreateDocument("attempt:squat-3", """{"lifterId":"lifter:1","liftName":"squat","attemptNumber":"3","weight":140}"""),
            CreateDocument("attempt:bench-1", """{"lifterId":"lifter:1","liftName":"bench","attemptNumber":"1","weight":80}"""),
            CreateDocument("attempt:dead-2", """{"lifterId":"lifter:1","liftName":"dead","attemptNumber":"2","weight":170}"""),
            CreateDocument("attempt:squat-1", """{"lifterId":"lifter:1","liftName":"squat","attemptNumber":"1","weight":120}"""),
            CreateDocument("attempt:bench-3", """{"lifterId":"lifter:1","liftName":"bench","attemptNumber":"3","weight":90}"""),
            CreateDocument("attempt:dead-1", """{"lifterId":"lifter:1","liftName":"dead","attemptNumber":"1","weight":160}""")
        };

        var details = CompetitionDetailsMapper.Create(
            new Competition { Id = "meet-1", Name = "Completed meet" },
            documents,
            DateTimeOffset.UtcNow);

        var lifter = Assert.Single(details.Lifters);
        Assert.Equal(
            new[] { "1", "1", "1", "2", "3", "3" },
            lifter.Attempts.Select(attempt => attempt.AttemptNumber?.GetString()));
        Assert.Equal(
            new[] { "bench", "dead", "squat", "dead", "bench", "squat" },
            lifter.Attempts.Select(attempt => attempt.LiftName));
    }

    [Fact]
    public void SerializedDetailsExposeOnlyExplicitPrivacySafeDtoFields()
    {
        var competition = new Competition { Id = "meet-1", Name = "Privacy Test" };
        var documents = new[]
        {
            CreateDocument(
                "lifter:1",
                """
                {
                  "name": "Public Name",
                  "divisions": [],
                  "bodyWeight": 75,
                  "birthDate": "private-birth-date",
                  "stripePaymentIntent": "private-payment",
                  "contactEmail": "private@example.test",
                  "changes": [{ "attribute": "name", "value": "private-history" }]
                }
                """)
        };

        var response = JsonSerializer.Serialize(
            CompetitionDetailsMapper.Create(competition, documents, DateTimeOffset.UtcNow),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("Public Name", response);
        Assert.Contains("bodyWeight", response);
        Assert.DoesNotContain("birthDate", response);
        Assert.DoesNotContain("private-birth-date", response);
        Assert.DoesNotContain("stripePaymentIntent", response);
        Assert.DoesNotContain("private-payment", response);
        Assert.DoesNotContain("contactEmail", response);
        Assert.DoesNotContain("private@example.test", response);
        Assert.DoesNotContain("changes", response);
        Assert.DoesNotContain("private-history", response);
    }

    [Fact]
    public void PlatformDashboardFindsMultiplePlatformsAndJoinsCurrentAttemptToLifter()
    {
        var documents = new[]
        {
            CreateDocument(
                "lifter:1",
                """
                {
                  "name": "Lifter One",
                  "flight": "A",
                  "session": 1,
                  "lot": 12,
                  "bodyWeight": 82.5,
                  "birthDate": "private-birth-date"
                }
                """),
            CreateDocument(
                "attempt:1",
                """
                {
                  "lifterId": "lifter:1",
                  "liftName": "Squat",
                  "attemptNumber": "2",
                  "weight": 120,
                  "result": "good",
                  "changes": [{ "value": "private-history" }]
                }
                """),
            CreateDocument(
                "platform:1",
                """
                {
                  "name": "Platform A",
                  "clockState": "running",
                  "clockTimerLength": 60,
                  "currentAttemptId": "attempt:1"
                }
                """),
            CreateDocument(
                "platform:2",
                """{"name":"Platform B","currentAttemptId":null}"""),
            CreateDocument(
                "platform-search:1",
                """{"name":"Search record","platformId":"platform:1","search":"platform"}""")
        };
        var syncedAt = DateTimeOffset.Parse("2026-10-22T01:00:00Z");

        var dashboard = CompetitionDetailsMapper.CreatePlatformDashboard("meet-1", documents, syncedAt);

        Assert.Equal(syncedAt, dashboard.LastSyncedAtUtc);
        Assert.Equal(new[] { "platform:1", "platform:2" }, dashboard.Platforms.Select(platform => platform.PlatformId));
        var platform = dashboard.Platforms[0];
        Assert.Equal("Platform A", platform.Name);
        Assert.Equal("running", platform.ClockState?.GetString());
        Assert.Equal("60", platform.ClockTimerLength?.GetRawText());
        var currentAttempt = Assert.IsType<CompetitionPlatformCurrentAttemptDto>(platform.CurrentAttempt);
        Assert.Equal("attempt:1", currentAttempt.AttemptId);
        Assert.Equal("Squat", currentAttempt.LiftName);
        Assert.Equal("2", currentAttempt.AttemptNumber?.GetString());
        Assert.Equal("120", currentAttempt.Weight?.GetRawText());
        Assert.Equal("Lifter One", currentAttempt.Lifter?.Name);
        Assert.Equal("A", currentAttempt.Lifter?.Flight);
        Assert.Equal(12, currentAttempt.Lifter?.Lot?.GetInt32());
    }

    [Fact]
    public void PlatformDashboardPreservesUnknownAttemptAndLifterReferences()
    {
        var documents = new[]
        {
            CreateDocument(
                "platform:unknown-lifter",
                """{"currentAttemptId":"attempt:unknown-lifter"}"""),
            CreateDocument(
                "attempt:unknown-lifter",
                """{"lifterId":"lifter:missing","liftName":"Bench","attemptNumber":1,"weight":80}"""),
            CreateDocument(
                "platform:missing-attempt",
                """{"currentAttemptId":"attempt:not-stored","clockTimerLength":null}"""),
            CreateDocument(
                "platform:completed",
                """{"clockState":null,"currentAttemptId":null}""")
        };

        var dashboard = CompetitionDetailsMapper.CreatePlatformDashboard(
            "meet-1",
            documents,
            DateTimeOffset.Parse("2026-10-22T01:00:00Z"));

        var missingLifterPlatform = Assert.Single(
            dashboard.Platforms,
            platform => platform.PlatformId == "platform:unknown-lifter");
        Assert.Null(missingLifterPlatform.ClockState);
        Assert.Null(missingLifterPlatform.ClockTimerLength);
        Assert.NotNull(missingLifterPlatform.CurrentAttempt);
        Assert.Null(missingLifterPlatform.CurrentAttempt.Lifter);

        var missingAttemptPlatform = Assert.Single(
            dashboard.Platforms,
            platform => platform.PlatformId == "platform:missing-attempt");
        Assert.Equal("attempt:not-stored", missingAttemptPlatform.CurrentAttemptId);
        Assert.Null(missingAttemptPlatform.CurrentAttempt);
        Assert.Equal(JsonValueKind.Null, missingAttemptPlatform.ClockTimerLength?.ValueKind);

        var completedPlatform = Assert.Single(
            dashboard.Platforms,
            platform => platform.PlatformId == "platform:completed");
        Assert.Null(completedPlatform.CurrentAttemptId);
        Assert.Null(completedPlatform.CurrentAttempt);
    }

    [Fact]
    public void PlatformDashboardSerializationContainsNoRawDocumentsOrSensitiveFields()
    {
        var documents = new[]
        {
            CreateDocument(
                "lifter:1",
                """
                {
                  "name": "Public Name",
                  "flight": "A",
                  "birthDate": "private-birth-date",
                  "paymentReference": "private-payment"
                }
                """),
            CreateDocument(
                "attempt:1",
                """{"lifterId":"lifter:1","liftName":"Deadlift","attemptNumber":"1","weight":180,"result":"good","contactEmail":"private@example.test"}"""),
            CreateDocument(
                "platform:1",
                """{"name":"Platform","currentAttemptId":"attempt:1","clockTimerLength":90}""")
        };

        var response = JsonSerializer.Serialize(
            CompetitionDetailsMapper.CreatePlatformDashboard(
                "meet-1",
                documents,
                DateTimeOffset.Parse("2026-10-22T01:00:00Z")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("Public Name", response);
        Assert.Contains("clockTimerLength", response);
        Assert.DoesNotContain("birthDate", response);
        Assert.DoesNotContain("private-birth-date", response);
        Assert.DoesNotContain("paymentReference", response);
        Assert.DoesNotContain("private-payment", response);
        Assert.DoesNotContain("contactEmail", response);
        Assert.DoesNotContain("private@example.test", response);
        Assert.DoesNotContain("changes", response);
        Assert.DoesNotContain("result", response);
    }

    private static CompetitionDocument CreateDocument(string id, string dataJson) =>
        new()
        {
            CompetitionId = "meet-1",
            DocumentId = id,
            Revision = "1-revision",
            DocumentDataJson = dataJson,
            LastSyncedAt = DateTimeOffset.Parse("2026-10-22T00:00:00Z")
        };
}
