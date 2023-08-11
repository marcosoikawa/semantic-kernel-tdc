﻿// Copyright (c) Microsoft. All rights reserved.

namespace PlayFabExamples.Example01_DataQnA.Reports;

public class DailyOverviewReportRecord
{
    public static string GetHeader() =>
        "Timestamp,TotalLogins,UniqueLogins,UniquePayers,Revenue,Purchases,TotalCalls,TotalSuccessfulCalls,TotalErrors,Arpu,Arppu,AvgPurchasePrice,NewUsers";

    public string AsCsvRow() =>
        $"{this.Timestamp},{this.TotalLogins},{this.UniqueLogins},{this.UniquePayers},{this.Revenue},{this.Purchases},{this.TotalCalls},{this.TotalSuccessfulCalls},{this.TotalErrors},{this.Arpu},{this.Arppu},{this.AvgPurchasePrice},{this.NewUsers}";

    public static string GetDescription() =>
        """
        The provided CSV table contains granular daily data capturing game reports for each hour, offering valuable insights into player engagement, financial performance, and the overall gameplay experience.
        This dataset offers a comprehensive view into player behavior, enabling data-driven decisions to enhance gameplay, optimize monetization strategies, and improve overall player satisfaction.
        Through its hour-by-hour breakdown, it allows for precise analysis of temporal patterns, aiding in understanding player dynamics over different times of the day.
        The report has 24 rows where every row reprsents one hour of the day.
        Description of Columns:
        - Timestamp: The date and time of a one-hour window when the report was compiled, presented in Coordinated Universal Time (UTC).
        - TotalLogins: The aggregate count of player logins during the specified hour, revealing the volume of player interactions.
        - UniqueLogins: The distinct number of players who logged into the game within the same hour, indicating individual engagement.
        - UniquePayers: The count of unique players who conducted in-game purchases, reflecting the game's monetization reach.
        - Revenue: The cumulative revenue in dollars generated from in-game purchases throughout the hour, demonstrating financial performance.
        - Purchases: The total number of in-game transactions carried out by players in the specified hour.
        - TotalCalls: The collective sum of player-initiated interactions, encompassing gameplay actions, API requests, and more..
        - TotalSuccessfulCalls: The count of interactions that succeeded without encountering errors, highlighting player satisfaction.
        - TotalErrors: The overall number of errors encountered during interactions, potential indicators of player experience challenges.
        - Arpu (Average Revenue Per User): The average revenue generated per unique player, calculated as Revenue / UniquePayers.
        - Arppu (Average Revenue Per Paying User): The average revenue generated per player who made purchases, calculated as Revenue / UniquePayers.
        - AvgPurchasePrice: The average price of in-game purchases made by players, calculated as Revenue / Purchases.
        - NewUsers: The count of new players who started engaging with the game during the specified hour period.
        """;

    public DateTime Timestamp { get; set; }
    public int TotalLogins { get; set; }
    public int UniqueLogins { get; set; }
    public int UniquePayers { get; set; }
    public decimal Revenue { get; set; }
    public int Purchases { get; set; }
    public long TotalCalls { get; set; }
    public long TotalSuccessfulCalls { get; set; }
    public long TotalErrors { get; set; }
    public decimal Arpu { get; set; }
    public decimal Arppu { get; set; }
    public decimal AvgPurchasePrice { get; set; }
    public int NewUsers { get; set; }
}
