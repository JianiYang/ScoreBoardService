using Microsoft.AspNetCore.Mvc;
using ScoreBoard.Models;
using ScoreBoard.Services;

namespace ScoreBoard.Controllers;

[ApiController]
[Route("customer")]
public class TestController : ControllerBase
{
    /// <summary>
    /// Test.
    /// </summary>
    [HttpPost("/num/{count:int}")]
    public ActionResult<decimal> testTime(int count)
    {
        var cache = SortedScoreBoard.Instance;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var rand = new Random();
        int first = rand.Next(count);
        int second = rand.Next(count);
        while (second == first && count > 1)
        {
            second = rand.Next(count);
        }

        string message;
        for (int i = 0; i < count; i++)
        {
            cache.AddOrUpdate(i, i % 10000, out message);
        }

        stopwatch.Stop();
        Console.WriteLine($"Init time cost: {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.Restart();
        return Ok("test");
    }
}
