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
    public void MapsUnitGenderLiftAndWeightClassConfigurationForCalculations()
    {
        var documents = new[]
        {
            CreateDocument(
                "division:open",
                """
                {
                  "name": "Open",
                  "gender": "FEMALE",
                  "rawOrEquipped": "RAW",
                  "scoreBy": "TOTAL",
                  "lifts": { "squat": true, "bench": true, "dead": true },
                  "weightClasses": {
                    "weight:83": { "_id": "weight:83", "name": "83", "maxWeight": 83 }
                  }
                }
                """),
            CreateDocument("meet:metadata", """{"units":"KG"}"""),
            CreateDocument(
                "lifter:1",
                """
                {
                  "name": "Athlete",
                  "gender": "FEMALE",
                  "bodyWeight": 82,
                  "divisions": [
                    {
                      "divisionId": "division:open",
                      "declaredAwardsWeightClassId": "weight:83",
                      "rawOrEquipped": "RAW"
                    }
                  ]
                }
                """),
            CreateDocument("attempt:squat", """{"lifterId":"lifter:1","liftName":"squat","attemptNumber":"1","weight":100,"result":"good"}"""),
            CreateDocument("attempt:bench", """{"lifterId":"lifter:1","liftName":"bench","attemptNumber":"1","weight":80,"result":"good"}"""),
            CreateDocument("attempt:dead", """{"lifterId":"lifter:1","liftName":"dead","attemptNumber":"1","weight":120,"result":"good"}""")
        };

        var details = CompetitionDetailsMapper.Create(
            new Competition { Id = "meet-1", Name = "Meet" },
            documents,
            DateTimeOffset.UtcNow);

        Assert.Equal("KG", details.Units);
        var lifter = Assert.Single(details.Lifters);
        var division = Assert.Single(lifter.Divisions);
        Assert.Equal("FEMALE", division.Gender);
        Assert.Equal("RAW", division.RawOrEquipped);
        Assert.Equal("TOTAL", division.ScoreBy);
        Assert.Equal(83, division.WeightClassMaxWeight);
        Assert.Equal("83", division.WeightClassName);
        Assert.Equal(true, division.Lifts.Squat);
        Assert.Equal(true, division.Lifts.Bench);
        Assert.Equal(true, division.Lifts.Dead);
        Assert.Equal(300, lifter.Performance.CompleteThreeLiftTotalKg);
    }

    [Fact]
    public void MapsConfirmedAndProjectedPointsPerDivisionAndKeepsBenchOnlyApplicability()
    {
        var documents = new[]
        {
            CreateDocument(
                "division:power",
                """
                {
                  "name": "Powerlifting",
                  "gender": "MALE",
                  "rawOrEquipped": "RAW",
                  "lifts": { "squat": true, "bench": true, "dead": true },
                  "weightClasses": {
                    "weight:83": { "_id": "weight:83", "name": "83 kg" }
                  }
                }
                """),
            CreateDocument(
                "division:bench",
                """
                {
                  "name": "Bench",
                  "gender": "MALE",
                  "rawOrEquipped": "EQUIPPED",
                  "lifts": { "squat": false, "bench": true, "dead": false },
                  "weightClasses": {
                    "weight:120": { "_id": "weight:120", "name": "120 kg" }
                  }
                }
                """),
            CreateDocument("meet:metadata", """{"units":"KG"}"""),
            CreateDocument(
                "lifter:scored",
                """
                {
                  "name": "Scored lifter",
                  "bodyWeight": 100,
                  "divisions": [
                    { "divisionId": "division:power", "declaredAwardsWeightClassId": "weight:83" },
                    { "divisionId": "division:bench", "declaredAwardsWeightClassId": "weight:120" }
                  ]
                }
                """),
            CreateDocument("attempt:squat", """{"lifterId":"lifter:scored","liftName":"squat","attemptNumber":"1","weight":200,"result":"good"}"""),
            CreateDocument("attempt:squat-pending", """{"lifterId":"lifter:scored","liftName":"squat","attemptNumber":"2","weight":210}"""),
            CreateDocument("attempt:bench", """{"lifterId":"lifter:scored","liftName":"bench","attemptNumber":"1","weight":100,"result":"good"}"""),
            CreateDocument("attempt:dead", """{"lifterId":"lifter:scored","liftName":"dead","attemptNumber":"1","weight":200,"result":"good"}""")
        };

        var details = CompetitionDetailsMapper.Create(
            new Competition { Id = "meet-1", Name = "Scored meet" },
            documents,
            DateTimeOffset.UtcNow);
        var lifter = Assert.Single(details.Lifters);
        var powerlifting = Assert.Single(lifter.Divisions, division => division.DivisionId == "division:power");
        var bench = Assert.Single(lifter.Divisions, division => division.DivisionId == "division:bench");

        Assert.Equal(63.164622576551416, powerlifting.PointsScores!["ipf-gl"].ConfirmedScore!.Value, 9);
        Assert.Equal(64.42791502808245, powerlifting.PointsScores["ipf-gl"].ProjectedScore!.Value, 9);
        Assert.NotNull(powerlifting.PointsScores["dots"].ConfirmedScore);
        Assert.NotNull(bench.PointsScores!["ipf-gl"].ConfirmedScore);
        Assert.Null(bench.PointsScores["dots"].ConfirmedScore);
        Assert.Null(bench.PointsScores["wilks-1"].ConfirmedScore);
    }

    [Fact]
    public void LeavesScoresUnavailableWhenBodyweightOrCompletePerformanceIsMissing()
    {
        var documents = new[]
        {
            CreateDocument(
                "division:power",
                """
                {
                  "gender": "FEMALE",
                  "rawOrEquipped": "RAW",
                  "lifts": { "squat": true, "bench": true, "dead": true },
                  "weightClasses": {}
                }
                """),
            CreateDocument("meet:metadata", """{"units":"KG"}"""),
            CreateDocument(
                "lifter:missing",
                """{"divisions":[{"divisionId":"division:power","declaredAwardsWeightClassId":"missing"}]}"""),
            CreateDocument("attempt:squat", """{"lifterId":"lifter:missing","liftName":"squat","attemptNumber":"1","weight":100,"result":"good"}"""),
            CreateDocument("attempt:bench", """{"lifterId":"lifter:missing","liftName":"bench","attemptNumber":"1","weight":80,"result":"good"}""")
        };

        var details = CompetitionDetailsMapper.Create(
            new Competition { Id = "meet-1", Name = "Incomplete meet" },
            documents,
            DateTimeOffset.UtcNow);
        var division = Assert.Single(Assert.Single(details.Lifters).Divisions);

        Assert.All(
            division.PointsScores!.Values,
            score =>
            {
                Assert.Null(score.ConfirmedScore);
                Assert.Null(score.ProjectedScore);
            });
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
