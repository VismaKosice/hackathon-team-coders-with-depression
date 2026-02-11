using PensionCalculationEngine.Models.Request;
using PensionCalculationEngine.Models.Response;
using PensionCalculationEngine.Models.Domain;
using System.Diagnostics;

namespace PensionCalculationEngine.Services;

public class CalculationService
{
    public async Task<CalculationResponse> ProcessCalculationRequestAsync(
        CalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var response = new CalculationResponse
        {
            CalculationMetadata = new CalculationMetadata
            {
                CalculationId = Guid.NewGuid().ToString(),
                TenantId = request.TenantId,
                CalculationStartedAt = startTime,
                CalculationOutcome = "SUCCESS"
            },
            CalculationResult = new CalculationResult
            {
                Messages = new List<CalculationMessage>(),
                Mutations = new List<ProcessedMutation>(),
                InitialSituation = new InitialSituation
                {
                    ActualAt = request.CalculationInstructions.Mutations.FirstOrDefault()?.ActualAt ?? DateOnly.MinValue,
                    Situation = new { dossier = (object?)null }
                }
            }
        };

        var currentSituation = new Situation { Dossier = null };
        var messages = new List<CalculationMessage>();
        var processedMutations = new List<ProcessedMutation>();
        
        string? lastSuccessfulMutationId = null;
        int lastSuccessfulMutationIndex = -1;
        DateOnly lastSuccessfulActualAt = response.CalculationResult.InitialSituation.ActualAt;

        for (int i = 0; i < request.CalculationInstructions.Mutations.Count; i++)
        {
            var mutation = request.CalculationInstructions.Mutations[i];
            var messageStartIndex = messages.Count;

            // Process mutation (placeholder logic)
            var mutationMessages = await ProcessMutationAsync(mutation, currentSituation, cancellationToken);
            messages.AddRange(mutationMessages);

            var messageIndexes = new List<int>();
            for (int j = messageStartIndex; j < messages.Count; j++)
            {
                messageIndexes.Add(j);
            }

            processedMutations.Add(new ProcessedMutation
            {
                Mutation = mutation,
                CalculationMessageIndexes = messageIndexes.Count > 0 ? messageIndexes : null
            });

            // Check for CRITICAL errors
            if (mutationMessages.Any(m => m.Severity == "CRITICAL"))
            {
                response.CalculationMetadata.CalculationOutcome = "FAILURE";
                break;
            }

            // Update tracking for successful mutation
            lastSuccessfulMutationId = mutation.MutationId;
            lastSuccessfulMutationIndex = i;
            lastSuccessfulActualAt = mutation.ActualAt;
        }

        stopwatch.Stop();
        response.CalculationMetadata.CalculationCompletedAt = DateTime.UtcNow;
        response.CalculationMetadata.CalculationDurationMs = stopwatch.ElapsedMilliseconds;
        response.CalculationResult.Messages = messages;
        response.CalculationResult.Mutations = processedMutations;

        // Set end situation
        response.CalculationResult.EndSituation = new SituationSnapshot
        {
            MutationId = lastSuccessfulMutationId ?? request.CalculationInstructions.Mutations.FirstOrDefault()?.MutationId ?? string.Empty,
            MutationIndex = Math.Max(0, lastSuccessfulMutationIndex),
            ActualAt = lastSuccessfulActualAt,
            Situation = SerializeSituation(currentSituation)
        };

        return response;
    }

    private Task<List<CalculationMessage>> ProcessMutationAsync(
        CalculationMutation mutation,
        Situation situation,
        CancellationToken cancellationToken)
    {
        // Placeholder: mutation processing logic will be implemented by mutation handlers
        var messages = new List<CalculationMessage>();

        switch (mutation.MutationDefinitionName)
        {
            case "create_dossier":
                // Placeholder for create_dossier logic
                break;
            case "add_policy":
                // Placeholder for add_policy logic
                break;
            case "apply_indexation":
                // Placeholder for apply_indexation logic
                break;
            case "calculate_retirement_benefit":
                // Placeholder for calculate_retirement_benefit logic
                break;
            default:
                messages.Add(new CalculationMessage
                {
                    Code = "UNKNOWN_MUTATION",
                    Severity = "CRITICAL",
                    Message = $"Unknown mutation definition: {mutation.MutationDefinitionName}"
                });
                break;
        }

        return Task.FromResult(messages);
    }

    private object SerializeSituation(Situation situation)
    {
        if (situation.Dossier == null)
        {
            return new { dossier = (object?)null };
        }

        return new
        {
            dossier = new
            {
                dossier_id = situation.Dossier.DossierId,
                status = situation.Dossier.Status,
                retirement_date = situation.Dossier.RetirementDate,
                persons = situation.Dossier.Persons.Select(p => new
                {
                    person_id = p.PersonId,
                    role = p.Role,
                    name = p.Name,
                    birth_date = p.BirthDate
                }).ToList(),
                policies = situation.Dossier.Policies.Select(p => new
                {
                    policy_id = p.PolicyId,
                    scheme_id = p.SchemeId,
                    employment_start_date = p.EmploymentStartDate,
                    salary = p.Salary,
                    part_time_factor = p.PartTimeFactor,
                    attainable_pension = p.AttainablePension,
                    projections = p.Projections
                }).ToList()
            }
        };
    }
}
