using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;
using static MicroMES.ApiTests.Features.PlanningFeature.PlanningTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>Quality of a plan, comparable between engines.</summary>
/// <param name="TotalLatenessMinutes">Sum over the orders of max(0, end of the last job - requested end), in minutes.</param>
/// <param name="Makespan">End of the last planned job.</param>
/// <param name="AverageUtilization">Mean over the machines of worked minutes / (capacity x working minutes between now and the makespan), 0..1.</param>
/// <param name="Unplanned">Jobs left unplanned.</param>
internal sealed record PlanMetrics(decimal TotalLatenessMinutes, DateTime Makespan, decimal AverageUtilization, int Unplanned);

/// <summary>A fixed scenario of the comparison bench.</summary>
internal sealed record PlanningScenario(string Name, PlanningInput Input);

/// <summary>
/// Comparison bench: fixed, realistic scenarios and the metrics to judge an <see cref="IPlanningEngine"/>.
/// A future engine (e.g. an optimizer) is run on the same scenarios and compared with the baseline of the heuristic.
/// </summary>
internal static class PlanningBench
{
    public static PlanMetrics Measure(PlanningScenario scenario, IPlanningEngine engine)
    {
        var input = scenario.Input;
        var result = engine.Plan(input);
        var jobs = input.Jobs.ToDictionary(j => j.WorkOrderId);
        var calc = new WorkingTimeCalculator(input.Zone, TimeSpan.FromDays(800));

        var makespan = result.Planned.Count == 0 ? input.Now : result.Planned.Max(p => p.End);

        decimal lateness = 0;
        foreach (var order in result.Planned.GroupBy(p => jobs[p.WorkOrderId].OrderCode))
        {
            var due = order.Select(p => jobs[p.WorkOrderId].RequestedEnd).Where(d => d is not null).Min(); // the deadline belongs to the order
            if (due is { } d && order.Max(p => p.End) > d) lateness += (decimal)(order.Max(p => p.End) - d).TotalMinutes;
        }

        var utilizations = new List<decimal>();
        foreach (var m in input.Equipment)
        {
            var available = calc.WorkingMinutesBetween(m.Availability, input.Now, makespan).Value * Math.Max(1, m.Capacity);
            if (available <= 0) { utilizations.Add(0m); continue; }
            var worked = result.Planned.Where(p => p.EquipmentId == m.Id)
                .Sum(p => calc.WorkingMinutesBetween(m.Availability, p.Start < input.Now ? input.Now : p.Start, p.End).Value);
            utilizations.Add(Math.Min(1m, worked / available));
        }
        return new PlanMetrics(Math.Round(lateness, 0), makespan,
            utilizations.Count == 0 ? 0m : Math.Round(utilizations.Average(), 3), result.Unplanned.Count);
    }

    private static IAvailabilitySource Weekdays(int fromHour, int toHour)
        => Source(CalendarTestKit_Weekdays.Select(d => new WeeklySlot(d, T(fromHour), T(toHour))).ToList());

    private static readonly DayOfWeek[] CalendarTestKit_Weekdays = MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit.Weekdays;

    /// <summary>Workshop with queues: 8 orders of 3 operations (cut -> weld -> pack) on 2+1+2 machines.</summary>
    public static PlanningScenario WorkshopQueues()
    {
        var machines = new[] { Machine(1, "CUT-1"), Machine(2, "CUT-2"), Machine(3, "WELD-1"), Machine(4, "PACK-1"), Machine(5, "PACK-2") };
        var jobs = new List<PlanningJob>();
        var id = 1;
        for (var o = 0; o < 8; o++)
        {
            var due = N(2, 12).AddDays(o % 3); // Mon 12:00, Tue, Wed: the first ones cannot make it
            var cut = Job(id, 60 + 15 * (o % 3), [1, 2], order: $"OP-{o:00}", op: 10, reqEnd: due, defaultMachine: 1, orderId: o);
            var weld = Job(id + 1, 90 + 10 * (o % 4), [3], [id], order: $"OP-{o:00}", op: 20, reqEnd: due, orderId: o);
            var pack = Job(id + 2, 30, [4, 5], [id + 1], order: $"OP-{o:00}", op: 30, reqEnd: due, defaultMachine: 4, orderId: o);
            jobs.AddRange([cut, weld, pack]);
            id += 3;
        }
        return new("Officina con code", Input(jobs, machines));
    }

    /// <summary>Two concurrent orders on the same machines, the second one has the nearer deadline.</summary>
    public static PlanningScenario ConcurrentOrders()
    {
        var machines = new[] { Machine(1, "TAG-1"), Machine(2, "ASS-1") };
        var jobs = new List<PlanningJob>
        {
            Job(1, 240, [1], order: "BIG", op: 10, reqEnd: N(2, 17), orderId: 1),
            Job(2, 240, [2], [1], order: "BIG", op: 20, reqEnd: N(2, 17), orderId: 1),
            Job(3, 180, [1], order: "URGENT", op: 10, reqEnd: N(3, 17), orderId: 2),
            Job(4, 120, [2], [3], order: "URGENT", op: 20, reqEnd: N(3, 17), orderId: 2),
            Job(5, 60, [1], order: "SMALL", op: 10, reqEnd: N(2, 12), orderId: 3, priority: 0)
        };
        return new("Due ordini concorrenti con scadenze diverse", Input(jobs, machines));
    }

    /// <summary>Slow bottleneck machine with an alternative at a lower efficiency, and a capacity-2 oven.</summary>
    public static PlanningScenario BottleneckWithAlternatives()
    {
        var machines = new[]
        {
            Machine(1, "FRESA"), Machine(2, "PRESSA-1", efficiency: 100m), Machine(3, "PRESSA-2", efficiency: 70m),
            Machine(4, "FORNO", capacity: 2)
        };
        var jobs = new List<PlanningJob>();
        var id = 1;
        for (var o = 0; o < 6; o++)
        {
            jobs.Add(Job(id, 45, [1], order: $"B-{o}", op: 10, reqEnd: N(2, 12).AddDays(o % 2), orderId: o));
            jobs.Add(Job(id + 1, 120, [2, 3], [id], order: $"B-{o}", op: 20, reqEnd: N(2, 12).AddDays(o % 2), defaultMachine: 2, orderId: o));
            jobs.Add(Job(id + 2, 150, [4], [id + 1], order: $"B-{o}", op: 30, reqEnd: N(2, 12).AddDays(o % 2), orderId: o));
            id += 3;
        }
        return new("Colli di bottiglia con alternative", Input(jobs, machines));
    }

    /// <summary>Machines with different calendars (standard, early shift, Saturday opening) and a closure.</summary>
    public static PlanningScenario DifferentCalendars()
    {
        var saturday = Source(Weekdays(8, 12).WeeklySlots.Concat([new WeeklySlot(DayOfWeek.Saturday, T(8), T(12))]).ToList());
        var closedWednesday = Source(null, Closed(D(2026, 11, 4), D(2026, 11, 4)));
        var machines = new[]
        {
            Machine(1, "STD"), Machine(2, "EARLY", availability: Weekdays(6, 14)),
            Machine(3, "SAT", availability: saturday), Machine(4, "CLOSED-WED", availability: closedWednesday)
        };
        var jobs = new List<PlanningJob>();
        for (var o = 0; o < 5; o++)
        {
            var id = o * 3 + 1;
            jobs.Add(Job(id, 200, [1, 2], order: $"C-{o}", op: 10, reqEnd: N(3, 17), orderId: o));
            jobs.Add(Job(id + 1, 150, [3, 4], [id], order: $"C-{o}", op: 20, reqEnd: N(3, 17), orderId: o));
            jobs.Add(Job(id + 2, 60, [1, 2, 3, 4], [id + 1], order: $"C-{o}", op: 30, reqEnd: N(3, 17), orderId: o));
        }
        return new("Calendari diversi", Input(jobs, machines));
    }

    /// <summary>Locked jobs (agreed with the customer) on two machines: the others are planned around them, a capacity-2 oven is shared.</summary>
    public static PlanningScenario LockedJobs()
    {
        var machines = new[] { Machine(1, "TAG-1"), Machine(2, "ASS-1"), Machine(3, "FORNO", capacity: 2) };
        var jobs = new List<PlanningJob>
        {
            LockedJob(1, 1, N(2, 10), N(2, 12), order: "FIX-0", orderId: 1),
            LockedJob(2, 3, N(2, 9), N(2, 11), order: "FIX-1", orderId: 2),
            LockedJob(3, 2, N(3, 8), N(3, 10), [], order: "FIX-2", orderId: 3)
        };
        for (var o = 0; o < 4; o++)
        {
            var id = 10 + o * 3;
            jobs.Add(Job(id, 90, [1], order: $"L-{o}", op: 10, reqEnd: N(3, 17), orderId: 10 + o));
            jobs.Add(Job(id + 1, 60, [3], [id], order: $"L-{o}", op: 20, reqEnd: N(3, 17), orderId: 10 + o));
            jobs.Add(Job(id + 2, 45, [2], [id + 1], order: $"L-{o}", op: 30, reqEnd: N(3, 17), orderId: 10 + o));
        }
        return new("Lavori fissati", Input(jobs, machines));
    }

    public static IReadOnlyList<PlanningScenario> All()
        => [WorkshopQueues(), ConcurrentOrders(), BottleneckWithAlternatives(), DifferentCalendars(), LockedJobs()];
}
