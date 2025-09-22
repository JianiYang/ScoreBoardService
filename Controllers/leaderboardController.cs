using Microsoft.AspNetCore.Mvc;
using ScoreBoard.Models;
using ScoreBoard.Services;

namespace ScoreBoard.Controllers;

[ApiController]
[Route("leaderboard")]
public class leaderboardController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<CustomerScoreWithRank>> GetCustomersByRank([FromQuery] int start, [FromQuery] int end)
    {
        if (start <= 0 || end < start)
        {
            return BadRequest("Invalid start or end parameters.");
        }

        var cache = SortedScoreBoard.Instance;

        try
        {
            var result = cache.GeCustomerScoresBetweenIndices(start - 1, end - 1)
            .Select((customer, index) =>
            {
                var cs = customer as CustomerScore;
                return new CustomerScoreWithRank
                {
                    CustomerId = cs.CustomerId,
                    Score = cs.Score,
                    Rank = start + index // Caculate the real index
                };
            });
            return Ok(result);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
      
    }

    [HttpGet("{customerId:long}")]
    public ActionResult<IEnumerable<CustomerScoreWithRank>> GetCustomerById(long customerId, [FromQuery] int high = 0, [FromQuery] int low = 0)
    {
        var cache = SortedScoreBoard.Instance;
        try
        {
            var result = cache.GeCustomerScoresAroundKey(customerId, high, low, out int start)
            .Select((customer, index) =>
            {
                var cs = customer as CustomerScore;
                return new CustomerScoreWithRank
                {
                    CustomerId = cs.CustomerId,
                    Score = cs.Score,
                    Rank = start + index + 1 // Caculate the real index
                };
            });
            return Ok(result);
        }
        catch (KeyNotFoundException e)
        {
            return BadRequest(e.Message);
        }
    }
}
