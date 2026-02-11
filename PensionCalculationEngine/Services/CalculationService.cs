using PensionCalculationEngine.Models.Request;
using PensionCalculationEngine.Models.Response;
using PensionCalculationEngine.Models.Domain;
using System.Diagnostics;
using System.Text.Json;

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
        return mutation.MutationDefinitionName switch
        {
            "create_dossier" => ProcessCreateDossierAsync(mutation, situation, cancellationToken),
            "add_policy" => ProcessAddPolicyAsync(mutation, situation, cancellationToken),
            "apply_indexation" => ProcessApplyIndexationAsync(mutation, situation, cancellationToken),
            "calculate_retirement_benefit" => ProcessCalculateRetirementBenefitAsync(mutation, situation, cancellationToken),
            _ => Task.FromResult(new List<CalculationMessage>
            {
                new CalculationMessage
                {
                    Code = "UNKNOWN_MUTATION",
                    Severity = "CRITICAL",
                    Message = $"Unknown mutation definition: {mutation.MutationDefinitionName}"
                }
            })
        };
    }

    #region Mutation Processors

    private Task<List<CalculationMessage>> ProcessCreateDossierAsync(
        CalculationMutation mutation,
        Situation situation,
        CancellationToken cancellationToken)
    {
        var messages = new List<CalculationMessage>();

        var dossierId = GetStringProperty(mutation.MutationProperties, "dossier_id");
        var personId = GetStringProperty(mutation.MutationProperties, "person_id");
        var name = GetStringProperty(mutation.MutationProperties, "name");
        var birthDate = GetDateOnlyProperty(mutation.MutationProperties, "birth_date");

        if (situation.Dossier != null)
        {
            messages.Add(new CalculationMessage
            {
                Code = "DOSSIER_ALREADY_EXISTS",
                Severity = "CRITICAL",
                Message = "A dossier already exists in the situation"
            });
            return Task.FromResult(messages);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            messages.Add(new CalculationMessage
            {
                Code = "INVALID_NAME",
                Severity = "CRITICAL",
                Message = "Name cannot be empty or blank"
            });
            return Task.FromResult(messages);
        }

        if (birthDate == DateOnly.MinValue || birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            messages.Add(new CalculationMessage
            {
                Code = "INVALID_BIRTH_DATE",
                Severity = "CRITICAL",
                Message = "Birth date is not valid or is in the future"
            });
            return Task.FromResult(messages);
        }

        situation.Dossier = new Dossier
        {
            DossierId = dossierId,
            Status = "ACTIVE",
            RetirementDate = null,
            Persons = new List<Person>
            {
                new Person
                {
                    PersonId = personId,
                    Role = "PARTICIPANT",
                    Name = name,
                    BirthDate = birthDate
                }
            },
            Policies = new List<Policy>()
        };

        return Task.FromResult(messages);
    }

    private Task<List<CalculationMessage>> ProcessAddPolicyAsync(
        CalculationMutation mutation,
        Situation situation,
        CancellationToken cancellationToken)
    {
        var messages = new List<CalculationMessage>();

        if (situation.Dossier == null)
        {
            messages.Add(new CalculationMessage
            {
                Code = "DOSSIER_NOT_FOUND",
                Severity = "CRITICAL",
                Message = "No dossier exists in the situation"
            });
            return Task.FromResult(messages);
        }

        var schemeId = GetStringProperty(mutation.MutationProperties, "scheme_id");
        var employmentStartDate = GetDateOnlyProperty(mutation.MutationProperties, "employment_start_date");
        var salary = GetDecimalProperty(mutation.MutationProperties, "salary");
        var partTimeFactor = GetDecimalProperty(mutation.MutationProperties, "part_time_factor");

        if (salary < 0)
        {
            messages.Add(new CalculationMessage
            {
                Code = "INVALID_SALARY",
                Severity = "CRITICAL",
                Message = "Salary cannot be negative"
            });
            return Task.FromResult(messages);
        }

        if (partTimeFactor < 0 || partTimeFactor > 1)
        {
            messages.Add(new CalculationMessage
            {
                Code = "INVALID_PART_TIME_FACTOR",
                Severity = "CRITICAL",
                Message = "Part-time factor must be between 0 and 1"
            });
            return Task.FromResult(messages);
        }

        var duplicateExists = situation.Dossier.Policies.Any(p => 
            p.SchemeId == schemeId && p.EmploymentStartDate == employmentStartDate);

        if (duplicateExists)
        {
            messages.Add(new CalculationMessage
            {
                Code = "DUPLICATE_POLICY",
                Severity = "WARNING",
                Message = $"A policy with scheme_id '{schemeId}' and employment_start_date '{employmentStartDate}' already exists"
            });
        }

        var sequenceNumber = situation.Dossier.Policies.Count + 1;
        var policyId = $"{situation.Dossier.DossierId}-{sequenceNumber}";

        var policy = new Policy
        {
            PolicyId = policyId,
            SchemeId = schemeId,
            EmploymentStartDate = employmentStartDate,
            Salary = salary,
            PartTimeFactor = partTimeFactor,
            AttainablePension = null,
            Projections = null
        };

        situation.Dossier.Policies.Add(policy);

        return Task.FromResult(messages);
    }

    private Task<List<CalculationMessage>> ProcessApplyIndexationAsync(
        CalculationMutation mutation,
        Situation situation,
        CancellationToken cancellationToken)
    {
        var messages = new List<CalculationMessage>();

        if (situation.Dossier == null)
        {
            messages.Add(new CalculationMessage
            {
                Code = "DOSSIER_NOT_FOUND",
                Severity = "CRITICAL",
                Message = "No dossier exists in the situation"
            });
            return Task.FromResult(messages);
        }

        if (situation.Dossier.Policies.Count == 0)
        {
            messages.Add(new CalculationMessage
            {
                Code = "NO_POLICIES",
                Severity = "CRITICAL",
                Message = "Dossier has no policies"
            });
            return Task.FromResult(messages);
        }

        var percentage = GetDecimalProperty(mutation.MutationProperties, "percentage");
        var schemeIdFilter = GetNullableStringProperty(mutation.MutationProperties, "scheme_id");
        var effectiveBeforeFilter = GetNullableDateOnlyProperty(mutation.MutationProperties, "effective_before");

        var matchingPolicies = situation.Dossier.Policies.AsEnumerable();

        if (schemeIdFilter != null)
        {
            matchingPolicies = matchingPolicies.Where(p => p.SchemeId == schemeIdFilter);
        }

        if (effectiveBeforeFilter.HasValue)
        {
            matchingPolicies = matchingPolicies.Where(p => p.EmploymentStartDate < effectiveBeforeFilter.Value);
        }

        var policiesToUpdate = matchingPolicies.ToList();

        if (policiesToUpdate.Count == 0 && (schemeIdFilter != null || effectiveBeforeFilter.HasValue))
        {
            messages.Add(new CalculationMessage
            {
                Code = "NO_MATCHING_POLICIES",
                Severity = "WARNING",
                Message = "No policies match the specified criteria"
            });
            return Task.FromResult(messages);
        }

        var hasNegativeSalary = false;
        foreach (var policy in policiesToUpdate)
        {
            var newSalary = policy.Salary * (1 + percentage);
            if (newSalary < 0)
            {
                policy.Salary = 0;
                hasNegativeSalary = true;
            }
            else
            {
                policy.Salary = newSalary;
            }
        }

        if (hasNegativeSalary)
        {
            messages.Add(new CalculationMessage
            {
                Code = "NEGATIVE_SALARY_CLAMPED",
                Severity = "WARNING",
                Message = "One or more salaries were negative after indexation and have been clamped to 0"
            });
        }

        return Task.FromResult(messages);
    }

    private Task<List<CalculationMessage>> ProcessCalculateRetirementBenefitAsync(
        CalculationMutation mutation,
        Situation situation,
        CancellationToken cancellationToken)
    {
        var messages = new List<CalculationMessage>();

        if (situation.Dossier == null)
        {
            messages.Add(new CalculationMessage
            {
                Code = "DOSSIER_NOT_FOUND",
                Severity = "CRITICAL",
                Message = "No dossier exists in the situation"
            });
            return Task.FromResult(messages);
        }

        if (situation.Dossier.Policies.Count == 0)
        {
            messages.Add(new CalculationMessage
            {
                Code = "NO_POLICIES",
                Severity = "CRITICAL",
                Message = "Dossier has no policies"
            });
            return Task.FromResult(messages);
        }

        var retirementDate = GetDateOnlyProperty(mutation.MutationProperties, "retirement_date");
        var participant = situation.Dossier.Persons.FirstOrDefault(p => p.Role == "PARTICIPANT");

        if (participant == null)
        {
            messages.Add(new CalculationMessage
            {
                Code = "NO_PARTICIPANT",
                Severity = "CRITICAL",
                Message = "No participant found in dossier"
            });
            return Task.FromResult(messages);
        }

        var yearsPerPolicy = new List<(Policy policy, decimal years)>();
        decimal totalYears = 0;

        foreach (var policy in situation.Dossier.Policies)
        {
            var daysBetween = retirementDate.DayNumber - policy.EmploymentStartDate.DayNumber;
            var years = Math.Max(0, daysBetween / 365.25m);

            if (retirementDate < policy.EmploymentStartDate)
            {
                messages.Add(new CalculationMessage
                {
                    Code = "RETIREMENT_BEFORE_EMPLOYMENT",
                    Severity = "WARNING",
                    Message = $"Retirement date is before employment start date for policy {policy.PolicyId}"
                });
            }

            yearsPerPolicy.Add((policy, years));
            totalYears += years;
        }

        var ageAtRetirement = CalculateAge(participant.BirthDate, retirementDate);
        var isEligible = ageAtRetirement >= 65 || totalYears >= 40;

        if (!isEligible)
        {
            messages.Add(new CalculationMessage
            {
                Code = "NOT_ELIGIBLE",
                Severity = "CRITICAL",
                Message = $"Participant is not eligible for retirement (age: {ageAtRetirement}, years of service: {totalYears:F2})"
            });
            return Task.FromResult(messages);
        }

        if (totalYears == 0)
        {
            foreach (var policy in situation.Dossier.Policies)
            {
                policy.AttainablePension = 0;
            }
        }
        else
        {
            decimal weightedSalarySum = 0;
            foreach (var (policy, years) in yearsPerPolicy)
            {
                var effectiveSalary = policy.Salary * policy.PartTimeFactor;
                weightedSalarySum += effectiveSalary * years;
            }

            var weightedAverageSalary = weightedSalarySum / totalYears;
            var accrualRate = 0.02m;
            var annualPension = weightedAverageSalary * totalYears * accrualRate;

            foreach (var (policy, years) in yearsPerPolicy)
            {
                var policyPension = annualPension * (years / totalYears);
                policy.AttainablePension = policyPension;
            }
        }

        situation.Dossier.Status = "RETIRED";
        situation.Dossier.RetirementDate = retirementDate;

        return Task.FromResult(messages);
    }

    #endregion

    #region Helper Methods

    private string GetStringProperty(Dictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value))
            return string.Empty;

        if (value is JsonElement jsonElement)
            return jsonElement.GetString() ?? string.Empty;

        return value?.ToString() ?? string.Empty;
    }

    private string? GetNullableStringProperty(Dictionary<string, object> properties, string key)
    {
        if (!properties.ContainsKey(key))
            return null;

        var stringValue = GetStringProperty(properties, key);
        return string.IsNullOrEmpty(stringValue) ? null : stringValue;
    }

    private DateOnly GetDateOnlyProperty(Dictionary<string, object> properties, string key)
    {
        var stringValue = GetStringProperty(properties, key);
        return DateOnly.TryParse(stringValue, out var date) ? date : DateOnly.MinValue;
    }

    private DateOnly? GetNullableDateOnlyProperty(Dictionary<string, object> properties, string key)
    {
        if (!properties.ContainsKey(key))
            return null;

        var stringValue = GetStringProperty(properties, key);
        return DateOnly.TryParse(stringValue, out var date) ? date : null;
    }

    private decimal GetDecimalProperty(Dictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value))
            return 0m;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.TryGetDecimal(out var decimalValue))
                return decimalValue;
            if (jsonElement.TryGetDouble(out var doubleValue))
                return (decimal)doubleValue;
        }

        if (value is decimal d) return d;
        if (value is double db) return (decimal)db;
        if (value is int i) return i;
        if (value is long l) return l;

        return 0m;
    }

    private int CalculateAge(DateOnly birthDate, DateOnly currentDate)
    {
        var age = currentDate.Year - birthDate.Year;
        if (currentDate.Month < birthDate.Month || (currentDate.Month == birthDate.Month && currentDate.Day < birthDate.Day))
        {
            age--;
        }
        return age;
    }

    #endregion

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
