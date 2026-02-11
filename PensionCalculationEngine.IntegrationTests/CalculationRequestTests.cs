using Microsoft.AspNetCore.Mvc.Testing;
using PensionCalculationEngine.IntegrationTests.Models;
using PensionCalculationEngine.Models.Request;
using PensionCalculationEngine.Models.Response;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace PensionCalculationEngine.IntegrationTests;

public class CalculationRequestTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly string _testCasesPath;
    private const decimal NumericTolerance = 0.01m;

    public CalculationRequestTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _testCasesPath = Path.Combine(AppContext.BaseDirectory, "TestCases");
    }

    #region Core Tests (C01-C10)

    [Fact]
    public async Task C01_CreateDossier_Only()
    {
        await RunTestCase("C01-create-dossier.json");
    }

    [Fact]
    public async Task C02_AddPolicy_Single()
    {
        await RunTestCase("C02-add-single-policy.json");
    }

    [Fact]
    public async Task C03_AddPolicy_Multiple()
    {
        await RunTestCase("C03-add-multiple-policies.json");
    }

    [Fact]
    public async Task C04_ApplyIndexation_NoFilters()
    {
        await RunTestCase("C04-apply-indexation.json");
    }

    [Fact]
    public async Task C05_ApplyIndexation_SchemeFilter()
    {
        await RunTestCase("C05-indexation-scheme-filter.json");
    }

    [Fact]
    public async Task C06_ApplyIndexation_DateFilter()
    {
        await RunTestCase("C06-indexation-date-filter.json");
    }

    [Fact]
    public async Task C07_FullHappyPath()
    {
        await RunTestCase("C07-full-happy-path.json");
    }

    [Fact]
    public async Task C08_PartTimeRetirement()
    {
        await RunTestCase("C08-part-time-retirement.json");
    }

    [Fact]
    public async Task C09_Error_IneligibleRetirement()
    {
        await RunTestCase("C09-error-ineligible-retirement.json");
    }

    [Fact]
    public async Task C10_Error_NoDossier()
    {
        await RunTestCase("C10-error-no-dossier.json");
    }

    #endregion

    #region Warning Tests (C11-C14)

    [Fact]
    public async Task C11_Warning_DuplicatePolicy()
    {
        await RunTestCase("C11-warning-duplicate-policy.json");
    }

    [Fact]
    public async Task C12_Warning_NoMatchingPolicies()
    {
        await RunTestCase("C12-warning-no-matching-policies.json");
    }

    [Fact]
    public async Task C13_Warning_NegativeSalaryClamped()
    {
        await RunTestCase("C13-warning-negative-salary-clamped.json");
    }

    [Fact]
    public async Task C14_Warning_RetirementBeforeEmployment()
    {
        await RunTestCase("C14-warning-retirement-before-employment.json");
    }

    #endregion

    #region Bonus Tests

    [Fact(Skip = "Bonus feature not yet implemented")]
    public async Task B01_ProjectFutureBenefits()
    {
        await RunTestCase("B01-project-future-benefits.json");
    }

    #endregion

    #region Helper Methods

    private async Task RunTestCase(string fileName)
    {
        // Load test case
        var testCase = await LoadTestCase(fileName);

        // Send request
        var response = await _client.PostAsJsonAsync("/calculation-requests", testCase.Request);

        // Assert HTTP status
        Assert.Equal((HttpStatusCode)testCase.Expected.HttpStatus, response.StatusCode);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return;
        }

        // Parse response
        var actualResponse = await response.Content.ReadFromJsonAsync<CalculationResponse>();
        Assert.NotNull(actualResponse);

        // Validate calculation outcome
        Assert.Equal(testCase.Expected.CalculationOutcome, actualResponse.CalculationMetadata.CalculationOutcome);

        // Validate message count and messages
        Assert.Equal(testCase.Expected.MessageCount, actualResponse.CalculationResult.Messages.Count);
        ValidateMessages(testCase.Expected.Messages, actualResponse.CalculationResult.Messages);

        // Validate end situation metadata
        Assert.Equal(testCase.Expected.EndSituationMutationId, actualResponse.CalculationResult.EndSituation.MutationId);
        Assert.Equal(testCase.Expected.EndSituationMutationIndex, actualResponse.CalculationResult.EndSituation.MutationIndex);
        Assert.Equal(testCase.Expected.EndSituationActualAt, actualResponse.CalculationResult.EndSituation.ActualAt);

        // Validate mutations processed count
        Assert.Equal(testCase.Expected.MutationsProcessedCount, actualResponse.CalculationResult.Mutations.Count);

        // Validate end situation structure (deep comparison)
        ValidateEndSituation(testCase.Expected.EndSituation, actualResponse.CalculationResult.EndSituation.Situation);
    }

    private async Task<TestCase> LoadTestCase(string fileName)
    {
        var filePath = Path.Combine(_testCasesPath, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test case file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath);
        var testCase = JsonSerializer.Deserialize<TestCase>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(testCase);
        return testCase;
    }

    private void ValidateMessages(List<TestExpectedMessage> expected, List<CalculationMessage> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Severity, actual[i].Severity);
            Assert.Equal(expected[i].Code, actual[i].Code);
        }
    }

    private void ValidateEndSituation(object? expected, object? actual)
    {
        if (expected == null && actual == null)
        {
            return;
        }

        Assert.NotNull(expected);
        Assert.NotNull(actual);

        var expectedJson = JsonSerializer.Serialize(expected);
        var actualJson = JsonSerializer.Serialize(actual);

        var expectedNode = JsonNode.Parse(expectedJson);
        var actualNode = JsonNode.Parse(actualJson);

        CompareJsonNodes(expectedNode, actualNode, "$");
    }

    private void CompareJsonNodes(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected == null && actual == null)
        {
            return;
        }

        Assert.NotNull(expected);
        Assert.NotNull(actual);

        if (expected is JsonObject expectedObj && actual is JsonObject actualObj)
        {
            foreach (var kvp in expectedObj)
            {
                var key = kvp.Key;
                var expectedValue = kvp.Value;
                var actualValue = actualObj[key];

                CompareJsonNodes(expectedValue, actualValue, $"{path}.{key}");
            }
        }
        else if (expected is JsonArray expectedArr && actual is JsonArray actualArr)
        {
            Assert.Equal(expectedArr.Count, actualArr.Count);

            for (int i = 0; i < expectedArr.Count; i++)
            {
                CompareJsonNodes(expectedArr[i], actualArr[i], $"{path}[{i}]");
            }
        }
        else if (expected is JsonValue expectedVal && actual is JsonValue actualVal)
        {
            if (expectedVal.TryGetValue<decimal>(out var expectedDecimal) &&
                actualVal.TryGetValue<decimal>(out var actualDecimal))
            {
                var difference = Math.Abs(expectedDecimal - actualDecimal);
                Assert.True(difference <= NumericTolerance, 
                    $"Numeric value at {path} differs by {difference}. Expected: {expectedDecimal}, Actual: {actualDecimal}");
            }
            else if (expectedVal.TryGetValue<double>(out var expectedDouble) &&
                     actualVal.TryGetValue<double>(out var actualDouble))
            {
                var difference = Math.Abs(expectedDouble - actualDouble);
                Assert.True(difference <= (double)NumericTolerance,
                    $"Numeric value at {path} differs by {difference}. Expected: {expectedDouble}, Actual: {actualDouble}");
            }
            else
            {
                Assert.Equal(expectedVal.ToJsonString(), actualVal.ToJsonString());
            }
        }
        else
        {
            Assert.Equal(expected.ToJsonString(), actual.ToJsonString());
        }
    }

    #endregion
}
