﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Xunit;

namespace SemanticKernel.UnitTests.CoreSkills;

// TODO: allow clock injection and test all functions
public class TimeSkillTests
{
    [Fact]
    public void ItCanBeInstantiated()
    {
        // Act - Assert no exception occurs
        var _ = new TimeSkill();
    }

    [Fact]
    public void ItCanBeImported()
    {
        // Arrange
        var kernel = KernelBuilder.Create();

        // Act - Assert no exception occurs e.g. due to reflection
        kernel.ImportSkill(new TimeSkill(), "time");
    }

    [Fact]
    public void Days_Ago()
    {
        double interval = 2;
        DateTime expected = DateTime.Now.AddDays(-interval);
        TimeSkill skill = new TimeSkill();
        string result = skill.DaysAgo(interval.ToString());
        DateTime returned = DateTime.Parse(result);
        Assert.Equal(expected.Day, returned.Day);
        Assert.Equal(expected.Month, returned.Month);
        Assert.Equal(expected.Year, returned.Year);
    }


    [Fact]
    public void LastMatchingDayBadInput()
    {
        TimeSkill skill = new TimeSkill();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => skill.LastMatchingDay("not a day name"));
        Assert.Equal("dayName", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(DayOfWeekEnumerator))]
    public void LastMatchingDay(DayOfWeek dayName)
    {
        int steps = 0;
        DateTime date = DateTime.Now.Date.AddDays(-1);
        while (date.DayOfWeek != dayName && steps <= 7)
        {
            date = date.AddDays(-1);
            steps++;
        }
        bool found = date.DayOfWeek == dayName;
        Assert.True(found);

        TimeSkill skill = new TimeSkill();
        string result = skill.LastMatchingDay(dayName.ToString());
        DateTime returned = DateTime.Parse(result);
        Assert.Equal(date.Day, returned.Day);
        Assert.Equal(date.Month, returned.Month);
        Assert.Equal(date.Year, returned.Year);
    }

    public static IEnumerable<object[]> DayOfWeekEnumerator()
    {
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            yield return new object[] { day };
        }
    }
}
