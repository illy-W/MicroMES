using MicroMES.ApiFeatures.CalendarFeature.Domain;

namespace MicroMES.ApiFeatures.PlanningFeature.Engine;

/// <summary>
/// Forward, finite-capacity heuristic. Rules:
/// <list type="number">
/// <item><b>Locked jobs</b> (plan locked by the user) are fixed intervals like the running ones: never changed, they occupy capacity, successors wait for their end; a free predecessor ending after their start, or locked jobs beyond capacity, are reported as conflicts.</item>
/// <item><b>Fixed jobs first.</b> Running jobs do not move: they occupy their machine from the real start to the end computed
/// from the remaining minutes (from now, on the machine calendar, with efficiency). Machine <c>BusyIntervals</c> are fixed too.</item>
/// <item><b>Processing order.</b> List scheduling: among the jobs whose predecessors are done, the next one is the best by
/// priority (higher first), requested end (nearer first, none last), requested start (earlier first, none last), order code,
/// operation number, work order id. This respects dependencies, also across orders, and is deterministic.</item>
/// <item><b>Earliest start</b> = max(now, requested start of the order, end of every predecessor), then the first working
/// instant of the chosen machine and the first moment the machine has room.</item>
/// <item><b>Machine.</b> The assigned machine is kept when it is a candidate. Otherwise the candidate that finishes the job
/// first; ties: the default machine of the phase, then the machine code. A phase without machines is planned on the default
/// calendar, without capacity limit and without machine (null). A phase with machines and no candidate is unplanned.</item>
/// <item><b>Capacity N and non-preemptive.</b> A job occupies one continuous interval [start, end) that may include breaks and
/// closures (nobody works then); it is never split. The start is the first one ≥ earliest such that the number of occupied
/// intervals overlapping the job never reaches the capacity; candidate starts are the earliest start and the ends of
/// occupied intervals, each moved to the next working instant.</item>
/// <item><b>Duration</b> = standard minutes ÷ efficiency, consumed on the machine calendar (shifts, breaks, exceptions,
/// DST) with <see cref="WorkingTimeCalculator"/>. Instants are whole minutes (end rounded up).</item>
/// <item><b>Horizon.</b> A job that does not end by now + horizon, or whose calendar has no working time, is unplanned with a reason;
/// its successors are unplanned too (<see cref="UnplannedReasonCode.PredecessorUnplanned"/>). Dependency cycles are unplanned.</item>
/// <item><b>Reasons.</b> Each planned job lists why it does not start right away: predecessor, requested start, calendar
/// closed, machine busy, assigned machine not enabled.</item>
/// </list>
/// Complexity: O(n log n) for the queue plus, per job and candidate machine, a scan of that machine's intervals.
/// </summary>
public sealed class ForwardFiniteCapacityEngine : IPlanningEngine
{
    public PlanningResult Plan(PlanningInput input)
    {
        var run = new Run(input);
        return run.Execute();
    }

    /// <summary>UTC instant: Unspecified (e.g. read by SQLite) is taken as UTC, Local is converted.</summary>
    private static DateTime Utc(DateTime t) => t.Kind switch
    {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc)
    };

    private static DateTime CeilMinute(DateTime t)
    {
        t = Utc(t);
        var rest = t.Ticks % TimeSpan.TicksPerMinute;
        return rest == 0 ? t : t.AddTicks(TimeSpan.TicksPerMinute - rest);
    }

    private sealed class Run
    {
        private readonly PlanningInput _input;
        private readonly WorkingTimeCalculator _calc;
        private readonly DateTime _now;
        private readonly DateTime _horizonEnd;
        private readonly Dictionary<int, PlanningJob> _jobs = [];
        private readonly Dictionary<int, PlanningEquipment> _equipment = [];
        private readonly Dictionary<int, List<PlanningInterval>> _busy = [];
        private readonly Dictionary<int, DateTime> _end = [];
        private readonly HashSet<int> _failed = [];
        private readonly List<PlannedJob> _planned = [];
        private readonly List<UnplannedJob> _unplanned = [];

        public Run(PlanningInput input)
        {
            _input = input;
            _now = CeilMinute(input.Now);
            var horizon = input.Horizon ?? PlanningInput.DefaultHorizon;
            _horizonEnd = _now + horizon;
            // The calculator looks far enough ahead; the horizon itself is checked by the engine.
            _calc = new WorkingTimeCalculator(input.Zone, horizon + horizon + TimeSpan.FromDays(31));
            foreach (var j in input.Jobs) _jobs.TryAdd(j.WorkOrderId, j);
            foreach (var e in input.Equipment)
            {
                _equipment.TryAdd(e.Id, e);
                _busy[e.Id] = e.BusyIntervals.Select(b => new PlanningInterval(Utc(b.Start), Utc(b.End))).Where(b => b.End > b.Start).ToList();
            }
        }

        private static int CompareKeys(PlanningJob a, PlanningJob b)
        {
            var c = b.Priority.CompareTo(a.Priority);
            if (c != 0) return c;
            c = CompareNullsLast(a.RequestedEnd, b.RequestedEnd);
            if (c != 0) return c;
            c = CompareNullsLast(a.RequestedStart, b.RequestedStart);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.OrderCode, b.OrderCode);
            if (c != 0) return c;
            c = a.OperationNumber.CompareTo(b.OperationNumber);
            return c != 0 ? c : a.WorkOrderId.CompareTo(b.WorkOrderId);
        }

        private readonly Dictionary<int, (DateTime Deadline, int LockedId)> _deadlines = [];

        /// <summary>Jobs with a deadline (predecessors of a locked job) first, by deadline; then the usual keys.</summary>
        private int CompareWithDeadline(PlanningJob a, PlanningJob b)
        {
            var ha = _deadlines.TryGetValue(a.WorkOrderId, out var da);
            var hb = _deadlines.TryGetValue(b.WorkOrderId, out var db);
            if (ha != hb) return ha ? -1 : 1;
            if (ha)
            {
                var c = da.Deadline.CompareTo(db.Deadline);
                if (c != 0) return c;
            }
            return CompareKeys(a, b);
        }

        /// <summary>
        /// Latest start of each open, free transitive predecessor P of a locked job L (fixed start S): S minus the standard durations
        /// (minutes / efficiency of the assigned, else default, else first candidate machine; calendar-agnostic, so it is an
        /// approximation: breaks and closures are ignored) of the jobs on the longest path between P and L, P and L excluded.
        /// Minimum over all locked descendants. Used only to anticipate the chain in the processing order; conflicts are still
        /// reported from the real plan.
        /// </summary>
        private void ComputeDeadlines()
        {
            var succs = _jobs.Keys.ToDictionary(k => k, _ => new List<int>());
            foreach (var j in _jobs.Values)
                foreach (var p in j.PredecessorIds.Where(p => p != j.WorkOrderId && _jobs.ContainsKey(p)).Distinct())
                    succs[p].Add(j.WorkOrderId);
            if (!_jobs.Values.Any(j => j.Locked)) return;

            TimeSpan Dur(PlanningJob j)
            {
                var eqId = j.AssignedEquipmentId ?? j.DefaultEquipmentId ?? j.CandidateEquipmentIds.FirstOrDefault();
                var eff = _equipment.TryGetValue(eqId, out var m) ? m.EfficiencyPercent : 100m;
                return TimeSpan.FromMinutes((double)WorkingTimeCalculator.ApplyEfficiency(Math.Max(0m, j.StandardMinutes), eff));
            }

            // Latest start of a job as seen by its predecessors; null = no deadline. Memoized, cycle-safe.
            var memo = new Dictionary<int, (DateTime, int)?>();
            var visiting = new HashSet<int>();
            (DateTime, int)? LatestStart(int id)
            {
                var j = _jobs[id];
                if (j.Locked) return (CeilMinute(j.LockedStart ?? _now), id);
                if (IsFx(j)) return null;
                if (memo.TryGetValue(id, out var cached)) return cached;
                if (!visiting.Add(id)) return null;
                (DateTime, int)? best = null;
                foreach (var q in succs[id].OrderBy(x => x))
                {
                    var l = LatestStart(q);
                    if (l is null) continue;
                    var cand = (l.Value.Item1 - (_jobs[q].Locked ? TimeSpan.Zero : Dur(_jobs[q])), l.Value.Item2);
                    if (best is null || cand.Item1 < best.Value.Item1) best = cand;
                }
                visiting.Remove(id);
                memo[id] = best;
                return best;
            }

            // Deadline of P = latest start allowed to its successors, i.e. latest finish of P.
            foreach (var j in _jobs.Values)
            {
                if (IsFx(j)) continue;
                var d = LatestStart(j.WorkOrderId);
                if (d is not null) _deadlines[j.WorkOrderId] = d.Value;
            }
        }

        private static int CompareNullsLast(DateTime? a, DateTime? b)
            => a is null ? (b is null ? 0 : 1) : b is null ? -1 : a.Value.CompareTo(b.Value);

        public PlanningResult Execute()
        {
            // Edges only among jobs of the input; a fixed job never waits for anything.
            var preds = _jobs.ToDictionary(j => j.Key,
                j => IsFx(j.Value) ? [] : j.Value.PredecessorIds.Where(p => p != j.Key && _jobs.ContainsKey(p)).Distinct().ToList());
            var succs = _jobs.Keys.ToDictionary(k => k, _ => new List<int>());
            foreach (var (id, ps) in preds) foreach (var p in ps) succs[p].Add(id);
            var waiting = preds.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

            ComputeDeadlines();
            var queue = new PriorityQueue<int, PlanningJob>(Comparer<PlanningJob>.Create(CompareWithDeadline));
            foreach (var (id, n) in waiting)
                if (n == 0 && !IsFx(_jobs[id])) queue.Enqueue(id, _jobs[id]);

            var done = new HashSet<int>();
            void Complete(int id)
            {
                done.Add(id);
                foreach (var s in succs[id])
                    if (!IsFx(_jobs[s]) && --waiting[s] == 0) queue.Enqueue(s, _jobs[s]);
            }

            // 1. Fixed jobs occupy their machines before anything else.
            foreach (var job in _jobs.Values.Where(IsFx).OrderBy(j => j, Comparer<PlanningJob>.Create(CompareKeys)))
            {
                if (job.Locked) PlaceLocked(job); else PlaceFixed(job);
                Complete(job.WorkOrderId);
            }

            // 2. List scheduling of the others.
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var job = _jobs[id];
                var failedPred = preds[id].FirstOrDefault(_failed.Contains);
                if (failedPred != 0 || preds[id].Any(_failed.Contains))
                {
                    Fail(job, UnplannedReasonCode.PredecessorUnplanned,
                        $"Non pianificabile: dipende dal lavoro {Label(failedPred)}, che non può essere pianificato.", failedPred);
                }
                else Place(job, preds[id]);
                Complete(id);
            }

            // 3. Whatever is left is in a cycle or depends on one.
            var rest = _jobs.Keys.Where(k => !done.Contains(k)).ToHashSet();
            if (rest.Count > 0)
            {
                // Peel the jobs with no successor left in the set: they only depend on a cycle.
                var peeled = new HashSet<int>();
                bool changed;
                do
                {
                    changed = false;
                    foreach (var id in rest.Where(r => !peeled.Contains(r)).OrderBy(r => r).ToList())
                        if (!succs[id].Any(s => rest.Contains(s) && !peeled.Contains(s)))
                        {
                            peeled.Add(id);
                            changed = true;
                        }
                } while (changed);
                foreach (var id in rest.OrderBy(r => r))
                {
                    var job = _jobs[id];
                    if (peeled.Contains(id))
                    {
                        var by = preds[id].Where(rest.Contains).OrderBy(p => p).First();
                        Fail(job, UnplannedReasonCode.PredecessorUnplanned,
                            $"Non pianificabile: dipende dal lavoro {Label(by)}, coinvolto in una dipendenza circolare.", by);
                    }
                    else
                        Fail(job, UnplannedReasonCode.CyclicDependency,
                            "Non pianificabile: il lavoro fa parte di una dipendenza circolare tra lavori.");
                }
            }

            return new PlanningResult(_planned, _unplanned.OrderBy(u => u.WorkOrderId).ToList(), FindConflicts());
        }

        private static bool IsFx(PlanningJob j) => j.IsFixed || j.Locked;

        private readonly Dictionary<int, (int? Machine, DateTime Start, DateTime End)> _fixedIntervals = [];

        private void PlaceLocked(PlanningJob job)
        {
            var machine = job.AssignedEquipmentId is { } id && _equipment.TryGetValue(id, out var m) ? m : null;
            var start = CeilMinute(job.LockedStart ?? _now);
            var end = CeilMinute(job.LockedEnd ?? start);
            if (end < start) end = start;
            if (machine is not null && end > start) _busy[machine.Id].Add(new PlanningInterval(start, end));
            _end[job.WorkOrderId] = end;
            _fixedIntervals[job.WorkOrderId] = (machine?.Id, start, end);
            _planned.Add(new PlannedJob(job.WorkOrderId, machine?.Id, start, end, true,
                [new PlanningReason(PlanningReasonCode.Locked, "Fissato: non spostato")], true));
        }

        /// <summary>Conflicts of the locked jobs: a predecessor ending after their start, and locked/running jobs beyond capacity.</summary>
        private List<PlanningConflict> FindConflicts()
        {
            var conflicts = new List<PlanningConflict>();
            var locked = _jobs.Values.Where(j => j.Locked).OrderBy(j => j.WorkOrderId).ToList();
            foreach (var l in locked)
            {
                var start = _fixedIntervals[l.WorkOrderId].Start;
                foreach (var p in l.PredecessorIds.Where(p => p != l.WorkOrderId && _jobs.ContainsKey(p)).Distinct().OrderBy(p => p))
                {
                    if (!_end.TryGetValue(p, out var pe) || pe <= start) continue;
                    conflicts.Add(new PlanningConflict(PlanningConflictCode.PredecessorAfterLockedStart, l.WorkOrderId, p, null,
                        [l.WorkOrderId, p], start, pe, (int)Math.Ceiling((pe - start).TotalMinutes)));
                }
            }

            // Capacity: only fixed intervals the engine knows by id (locked and running jobs).
            var seen = new HashSet<string>();
            foreach (var group in _planned.Where(p => p.IsFixed && p.EquipmentId is not null).GroupBy(p => p.EquipmentId!.Value).OrderBy(g => g.Key))
            {
                var capacity = Math.Max(1, _equipment[group.Key].Capacity);
                var items = group.Where(p => p.End > p.Start).OrderBy(p => p.Start).ThenBy(p => p.WorkOrderId).ToList();
                foreach (var cur in items)
                {
                    // Jobs active at the start of cur.
                    var active = items.Where(p => p.Start <= cur.Start && p.End > cur.Start).OrderBy(p => p.WorkOrderId).ToList();
                    if (active.Count <= capacity || !active.Any(a => _jobs[a.WorkOrderId].Locked)) continue;
                    var key = group.Key + ":" + string.Join(",", active.Select(a => a.WorkOrderId));
                    if (!seen.Add(key)) continue;
                    var until = active.Min(a => a.End);
                    var anchor = active.Where(a => _jobs[a.WorkOrderId].Locked).Min(a => a.WorkOrderId);
                    conflicts.Add(new PlanningConflict(PlanningConflictCode.LockedOverCapacity, anchor, null, group.Key,
                        active.Select(a => a.WorkOrderId).ToList(), cur.Start, until, (int)Math.Ceiling((until - cur.Start).TotalMinutes)));
                }
            }
            return conflicts;
        }

        private string Label(int workOrderId)
            => _jobs.TryGetValue(workOrderId, out var j) ? $"{j.OrderCode}/{j.OperationNumber}" : workOrderId.ToString();

        private void Fail(PlanningJob job, UnplannedReasonCode code, string message, int? related = null)
        {
            _failed.Add(job.WorkOrderId);
            _unplanned.Add(new UnplannedJob(job.WorkOrderId, code, message, related is 0 ? null : related));
        }

        private void PlaceFixed(PlanningJob job)
        {
            _equipment.TryGetValue(job.AssignedEquipmentId ?? 0, out var machine);
            var start = job.FixedStart is { } fs ? fs : _now;
            start = CeilMinute(start);
            var from = start > _now ? start : _now;
            var source = machine?.Availability ?? _input.DefaultAvailability;
            var minutes = WorkingTimeCalculator.ApplyEfficiency(Math.Max(0m, job.StandardMinutes), machine?.EfficiencyPercent ?? 100m);
            var end = _calc.AddWorkingMinutes(source, from, minutes);
            var endAt = end.IsSuccess ? CeilMinute(end.Value) : from;
            if (endAt < start) endAt = start;
            if (machine is not null && endAt > start) _busy[machine.Id].Add(new PlanningInterval(start, endAt));
            _end[job.WorkOrderId] = endAt;
            _planned.Add(new PlannedJob(job.WorkOrderId, machine?.Id, start, endAt, true, []));
        }

        private sealed record Slot(PlanningEquipment? Machine, DateTime Start, DateTime End, UnplannedReasonCode? Error);

        private void Place(PlanningJob job, List<int> predecessors)
        {
            var reasons = new List<PlanningReason>();
            if (_deadlines.TryGetValue(job.WorkOrderId, out var dl))
                reasons.Add(new PlanningReason(PlanningReasonCode.AdvancedForLocked,
                    $"Anticipato per rispettare il lavoro fissato {Label(dl.LockedId)}", null, dl.LockedId));

            // Earliest start from now, the requested start and the predecessors.
            var ready = _now;
            if (job.RequestedStart is { } rs)
            {
                var r = CeilMinute(rs);
                if (r > ready) ready = r;
            }
            var afterRequested = ready;
            int? bindingPred = null;
            var predEnd = DateTime.MinValue;
            foreach (var p in predecessors.OrderBy(p => p))
                if (_end.TryGetValue(p, out var e) && e > predEnd) { predEnd = e; bindingPred = p; }
            if (job.ClosedPredecessorsEnd is { } closed)
            {
                var c = CeilMinute(closed);
                if (c > predEnd) { predEnd = c; bindingPred = null; }
            }
            if (predEnd > ready) ready = predEnd;

            // Candidate machines.
            var candidates = job.CandidateEquipmentIds.Distinct().Where(_equipment.ContainsKey).Select(id => _equipment[id]).ToList();
            var noMachine = candidates.Count == 0 && !job.PhaseHasMachines;
            if (candidates.Count == 0 && job.PhaseHasMachines)
            {
                Fail(job, UnplannedReasonCode.NoCandidateMachine,
                    "Non pianificabile: la fase ha macchine ma nessuna è attiva e abilitata. Abilita o attiva una macchina.");
                return;
            }
            if (job.AssignedEquipmentId is { } assigned && !noMachine)
            {
                var keep = candidates.FirstOrDefault(c => c.Id == assigned);
                if (keep is not null) candidates = [keep];
                else
                    reasons.Add(new PlanningReason(PlanningReasonCode.AssignedMachineNotEnabled,
                        "La macchina assegnata non è attiva o abilitata per la fase: scelta un'altra macchina."));
            }

            Slot? best = null;
            var firstError = UnplannedReasonCode.HorizonExceeded;
            var anyNoTime = false;
            var options = noMachine ? new List<PlanningEquipment?> { null } : candidates.Cast<PlanningEquipment?>().ToList();
            foreach (var machine in options)
            {
                var slot = FindSlot(machine, ready, job.StandardMinutes);
                if (slot.Error is { } err)
                {
                    if (err == UnplannedReasonCode.NoWorkingTime) anyNoTime = true;
                    continue;
                }
                if (best is null || Better(slot, best, job)) best = slot;
            }
            if (best is null)
            {
                firstError = anyNoTime ? UnplannedReasonCode.NoWorkingTime : UnplannedReasonCode.HorizonExceeded;
                Fail(job, firstError, firstError == UnplannedReasonCode.NoWorkingTime
                    ? "Non pianificabile: nessuna ora lavorabile nel calendario. Aggiungi fasce di lavoro o rimuovi le chiusure."
                    : "Non pianificabile: nessuna ora lavorabile entro l'orizzonte di pianificazione.");
                return;
            }

            // Reasons for the wait, in the order they apply.
            var source = best.Machine?.Availability ?? _input.DefaultAvailability;
            if (bindingPred is { } bp && predEnd > afterRequested)
                reasons.Add(new PlanningReason(PlanningReasonCode.WaitsForPredecessor,
                    $"Aspetta il lavoro {Label(bp)}, che finisce il {PlanningDateFormat.UntilToken}.", predEnd, bp));
            else if (predEnd > afterRequested)
                reasons.Add(new PlanningReason(PlanningReasonCode.WaitsForPredecessor,
                    $"Aspetta i lavori precedenti, che finiscono il {PlanningDateFormat.UntilToken}.", predEnd));
            else if (afterRequested > _now)
                reasons.Add(new PlanningReason(PlanningReasonCode.WaitsForRequestedStart,
                    $"Aspetta l'inizio richiesto dell'ordine, il {PlanningDateFormat.UntilToken}.", afterRequested));
            var first = _calc.NextWorkingInstant(source, ready);
            var firstWorking = first.IsSuccess ? first.Value : ready;
            if (firstWorking > ready)
                reasons.Add(new PlanningReason(PlanningReasonCode.CalendarClosed,
                    $"Il calendario{(best.Machine is null ? "" : $" di {best.Machine.Code}")} è chiuso fino al {PlanningDateFormat.UntilToken}.",
                    firstWorking, null, best.Machine?.Id));
            if (best.Start > firstWorking && best.Machine is not null)
                reasons.Add(new PlanningReason(PlanningReasonCode.MachineBusy,
                    $"La macchina {best.Machine.Code} è occupata fino al {PlanningDateFormat.UntilToken}.", best.Start, null, best.Machine.Id));

            if (best.Machine is not null && best.End > best.Start) _busy[best.Machine.Id].Add(new PlanningInterval(best.Start, best.End));
            _end[job.WorkOrderId] = best.End;
            _planned.Add(new PlannedJob(job.WorkOrderId, best.Machine?.Id, best.Start, best.End, false, reasons));
        }

        /// <summary>Earliest end wins; then the default machine of the phase; then the machine code.</summary>
        private static bool Better(Slot candidate, Slot best, PlanningJob job)
        {
            if (candidate.End != best.End) return candidate.End < best.End;
            var cd = candidate.Machine?.Id == job.DefaultEquipmentId;
            var bd = best.Machine?.Id == job.DefaultEquipmentId;
            if (cd != bd) return cd;
            return string.CompareOrdinal(candidate.Machine?.Code, best.Machine?.Code) < 0;
        }

        /// <summary>First [start, end) with room on the machine (or on the default calendar when machine is null).</summary>
        private Slot FindSlot(PlanningEquipment? machine, DateTime earliest, decimal standardMinutes)
        {
            var source = machine?.Availability ?? _input.DefaultAvailability;
            if (!AvailabilityResolver.HasAnyWorkingTime(source)) return new Slot(machine, earliest, earliest, UnplannedReasonCode.NoWorkingTime);
            var minutes = WorkingTimeCalculator.ApplyEfficiency(Math.Max(0m, standardMinutes), machine?.EfficiencyPercent ?? 100m);
            var capacity = machine is null ? int.MaxValue : Math.Max(1, machine.Capacity);
            var busy = machine is null ? null : _busy[machine.Id];

            var candidate = earliest;
            // Each retry moves past at least one interval end: more retries than intervals means something is wrong, never loop.
            for (var attempt = 0; attempt <= (busy?.Count ?? 0) + 1; attempt++)
            {
                var s = _calc.NextWorkingInstant(source, candidate);
                if (!s.IsSuccess) return new Slot(machine, earliest, earliest, UnplannedReasonCode.NoWorkingTime);
                var start = CeilMinute(s.Value);
                if (start >= _horizonEnd) return new Slot(machine, start, start, UnplannedReasonCode.HorizonExceeded);
                var e = _calc.AddWorkingMinutes(source, start, minutes);
                if (!e.IsSuccess) return new Slot(machine, start, start, UnplannedReasonCode.HorizonExceeded);
                var end = CeilMinute(e.Value);
                if (end > _horizonEnd) return new Slot(machine, start, end, UnplannedReasonCode.HorizonExceeded);

                if (busy is null || end == start || capacity == int.MaxValue) return new Slot(machine, start, end, null);
                var overlapping = busy.Where(b => b.Start < end && b.End > start).ToList();
                if (MaxDepth(overlapping, capacity) < capacity) return new Slot(machine, start, end, null);
                // Retry when something is released: the earliest end of the overlapping intervals.
                candidate = overlapping.Where(b => b.End > start).Min(b => b.End);
            }
            return new Slot(machine, candidate, candidate, UnplannedReasonCode.HorizonExceeded);
        }

        /// <summary>Highest number of intervals active at the same instant (sweep; ends before starts at the same instant).</summary>
        private static int MaxDepth(List<PlanningInterval> intervals, int stopAt)
        {
            if (intervals.Count < stopAt) return intervals.Count;
            var events = new List<(DateTime At, int Delta)>(intervals.Count * 2);
            foreach (var i in intervals)
            {
                events.Add((i.Start, 1));
                events.Add((i.End, -1));
            }
            events.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Delta.CompareTo(b.Delta));
            int depth = 0, max = 0;
            foreach (var (_, d) in events)
            {
                depth += d;
                if (depth > max) max = depth;
            }
            return max;
        }
    }
}
