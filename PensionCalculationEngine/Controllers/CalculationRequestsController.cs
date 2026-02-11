using Microsoft.AspNetCore.Mvc;
using PensionCalculationEngine.Models.Request;
using PensionCalculationEngine.Models.Response;
using PensionCalculationEngine.Services;

namespace PensionCalculationEngine.Controllers;

/// <summary>
/// Controller for processing pension calculation requests
/// </summary>
[ApiController]
[Route("calculation-requests")]
public class CalculationRequestsController : ControllerBase
{
    private readonly CalculationService _calculationService;

    public CalculationRequestsController(CalculationService calculationService)
    {
        _calculationService = calculationService;
    }

    /// <summary>
    /// Processes a pension calculation request with ordered mutations
    /// </summary>
    /// <param name="request">The calculation request containing tenant information and mutations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The calculation response with results and metadata</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CalculationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalculationResponse>> ProcessCalculationRequest(
        [FromBody] CalculationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ValidationProblemDetails(ModelState));
            }

            var response = await _calculationService.ProcessCalculationRequestAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception)
        {
            return StatusCode(500, new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while processing the calculation request"
            });
        }
    }
}
