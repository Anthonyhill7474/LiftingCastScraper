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
                  "bodyWeight": 82.5
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
        Assert.Empty(lifter.Divisions);
        var attempt = Assert.Single(lifter.Attempts);
        Assert.Null(attempt.AttemptNumber);
        Assert.Null(attempt.Weight);
        Assert.Null(attempt.Result);
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
