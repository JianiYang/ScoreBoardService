using Microsoft.AspNetCore.Mvc;
using ScoreBoard.Models;
using ScoreBoard.Services;

namespace ScoreBoard.Controllers;

[ApiController]
[Route("customer")]
public class CustomerControllerController : ControllerBase
{
    /// <summary>
    /// Addes or Updates the score for a specific customer.
    /// </summary>
    /// <param name="customerId">The unique identifier of the customer.</param>
    /// <param name="score">
    /// The score to set for the new customer or score changed value for exist customer.
    /// Must be within the range [-1000, +1000].
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{decimal}"/> containing a message with the updated score,
    /// or a <see cref="BadRequestResult"/> if the score is out of range.
    /// </returns>
    [HttpPost("{customerId:long}/score/{score:decimal}")]
    public ActionResult<decimal> UpdateScore(long customerId, decimal score)
    {
        // score range [-1000, +1000]
        if (score < -1000 || score > 1000)
        {
            return BadRequest("Score must be between -1000 and +1000.");
        }
        var cache = SortedScoreBoard.Instance;
        var newScore = cache.AddOrUpdate(customerId, score);
        return Ok($"User {customerId} has been set to new score {newScore}.");
    }
}
