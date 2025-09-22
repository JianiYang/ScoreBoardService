using System;

namespace ScoreBoard.Models;

public class CustomerScore
{
    public CustomerScore(long customerId, decimal score)
    {
        CustomerId = customerId;
        Score = score;
    }


    public long CustomerId { get; set; }

    public decimal Score { get; set; }
}

public class CustomerScoreComparer : IComparer<CustomerScore>
{
    // Score high -> low
    // CustomerId low -> high
    public int Compare(CustomerScore x, CustomerScore y)
    {
        int scoreCompare = y.Score.CompareTo(x.Score);
        if (scoreCompare != 0) return scoreCompare;

        return x.CustomerId.CompareTo(y.CustomerId);
    }
}
