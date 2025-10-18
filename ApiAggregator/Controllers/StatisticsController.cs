namespace ApiAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("3. Statistics")]
public class StatisticsController : ControllerBase
{
    private readonly ILogger<StatisticsController> _logger;
    private readonly IStatisticsService _statisticsService;
    public StatisticsController(ILogger<StatisticsController> logger, IStatisticsService statisticsService)
    {
        _logger = logger;
        _statisticsService = statisticsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiRequestStatistics), StatusCodes.Status200OK)] 
    public ActionResult GetStatistics()
    {
        try
        {
            _logger.LogInformation("Getting API request statistics.");
            var stats = _statisticsService.GetStatistics();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal server error: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
        }
    }
}
