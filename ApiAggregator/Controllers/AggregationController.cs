namespace ApiAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("2. Data Aggregation")]
public class AggregationController : ControllerBase
{
    private readonly ILogger<AggregationController> _logger; 
    private readonly IAggregationService _aggregationService;
    public AggregationController(ILogger<AggregationController> logger, IAggregationService aggregationService)
    {
        _logger = logger;
        _aggregationService = aggregationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AggregatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAggregatedData(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? sortBy = "date",
        [FromQuery] bool ascending = false,
        [FromQuery] int? maxResults = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new AggregationRequest
            {
                SearchTerm = searchTerm,
                Category = category,
                FromDate = fromDate,
                ToDate = toDate,
                SortBy = sortBy,
                Ascending = ascending,
                MaxResults = maxResults
            };
            _logger.LogInformation("Aggregating data with search term: {SearchTerm}", searchTerm);

            var result = await _aggregationService.AggregateDataAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal server error: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
        }
    }

    
}
