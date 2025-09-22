using System;

namespace ScoreBoard.Models;

public class CustomerScoreWithRank
{
    public long CustomerId { get; set; }

    public decimal Score { get; set; }
    
    public int Rank { get; set; }
}
