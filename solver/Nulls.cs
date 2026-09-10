using System.Collections.Concurrent;

// Nulls. D'Agapeyeff writes on p.111 that inserting dummy letters "at every third,
// fourth, or fifth letter" makes decipherment extremely difficult. Interleaved nulls
// destroy repeated n-grams without reordering anything, so they are an alternative
// explanation for the repeat profile that needs no transposition at all.
//
// Two models:
//   positional - every p-th unit (offset o) is a dummy; strike them and solve
//   symbolic   - some cells of the square are dummies; strike every occurrence
static class Nulls
{
    static (double per, string plain, int n) Solve(int[] units, IEnumerable<int> keepIdx, int restarts, int seed)
    {
        var kept = keepIdx.Select(i => units[i]).ToArray();
        if (kept.Length < 40) return (-9, "", kept.Length);
        var uniq = kept.Distinct().ToArray();
        if (uniq.Length > 26) return (-9, "", kept.Length);
        var map = uniq.Select((u, i) => (u, i)).ToDictionary(p => p.u, p => p.i);
        var seq = kept.Select(x => map[x]).ToArray();
        var fo = Joint.FreqOrder(seq, uniq.Length);
        var (per, plain) = Routes.MonoSolve(seq, uniq.Length, restarts, seed, fo);
        return (per, plain, kept.Length);
    }

    public static void Attack(string[] a)
    {
        string mode = a.Length > 1 ? a[1] : "all";
        int deep = a.Length > 2 ? int.Parse(a[2]) : 60;
        string digits = a.Length > 3 ? a[3] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int n = ct.Length, S = names.Length;
        Console.WriteLine($"NULL HUNT  {n} units, {S} distinct: {string.Join(" ", names)}");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");
        Console.WriteLine();

        var best = new List<(double per, string what, string plain, int len)>();

        // ---------- positional nulls: every p-th unit is a dummy
        if (mode == "all" || mode == "pos")
        {
            Console.WriteLine("POSITIONAL -- every p-th unit struck out");
            Console.WriteLine($"{"p",3} {"offset",7} {"kept",6} {"per letter",12}   plaintext");
            for (int p = 2; p <= 9; p++)
                for (int o = 0; o < p; o++)
                {
                    var keep = Enumerable.Range(0, n).Where(i => i % p != o);
                    var (per, plain, len) = Solve(ct, keep, deep, 31 + p * 17 + o);
                    Console.WriteLine($"{p,3} {o,7} {len,6} {per,12:F4}   {plain[..Math.Min(46, plain.Length)]}");
                    best.Add((per, $"every {p}th unit, offset {o}", plain, len));
                }
            Console.WriteLine();

            // the complement: keep only every p-th unit (the dummies were the message)
            Console.WriteLine("POSITIONAL -- keep ONLY every p-th unit");
            for (int p = 2; p <= 5; p++)
                for (int o = 0; o < p; o++)
                {
                    var keep = Enumerable.Range(0, n).Where(i => i % p == o);
                    var (per, plain, len) = Solve(ct, keep, deep, 77 + p * 13 + o);
                    if (len >= 40)
                    {
                        Console.WriteLine($"{p,3} {o,7} {len,6} {per,12:F4}   {plain[..Math.Min(46, plain.Length)]}");
                        best.Add((per, $"only every {p}th unit, offset {o}", plain, len));
                    }
                }
            Console.WriteLine();
        }

        // ---------- symbolic nulls: some cells of the square are dummies
        if (mode == "all" || mode == "sym")
        {
            Console.WriteLine("SYMBOLIC -- every occurrence of a chosen set of cells struck out");
            var results = new ConcurrentBag<(double per, string what, string plain, int len)>();
            var subsets = new List<int[]>();
            for (int k = 1; k <= 4; k++)
            {
                var idx = new int[k];
                void Rec(int d, int start)
                {
                    if (d == k) { subsets.Add((int[])idx.Clone()); return; }
                    for (int i = start; i < S; i++) { idx[d] = i; Rec(d + 1, i + 1); }
                }
                Rec(0, 0);
            }
            Console.WriteLine($"  {subsets.Count:N0} subsets of size 1-4");
            Parallel.ForEach(Partitioner.Create(0, subsets.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, range =>
                {
                    for (int si = range.Item1; si < range.Item2; si++)
                    {
                        var sub = subsets[si];
                        var bad = new bool[S];
                        foreach (var x in sub) bad[x] = true;
                        var keep = Enumerable.Range(0, n).Where(i => !bad[ct[i]]);
                        var (per, plain, len) = Solve(ct, keep, 2, 101 + si);
                        if (per > -9)
                            results.Add((per, "nulls = " + string.Join(",", sub.Select(x => names[x])), plain, len));
                    }
                });
            foreach (var r in results.OrderByDescending(x => x.per).Take(10))
                Console.WriteLine($"  {r.per,9:F4} {r.len,5} kept   {r.what,-22} {r.plain[..Math.Min(40, r.plain.Length)]}");
            best.AddRange(results.OrderByDescending(x => x.per).Take(20));
            Console.WriteLine();
        }

        // ---------- both together
        if (mode == "all" || mode == "both")
        {
            Console.WriteLine("COMBINED -- a positional stride AND one or two null cells");
            var results = new ConcurrentBag<(double per, string what, string plain, int len)>();
            var jobs = new List<(int p, int o, int[] sub)>();
            for (int p = 2; p <= 6; p++)
                for (int o = 0; o < p; o++)
                {
                    jobs.Add((p, o, new int[0]));
                    for (int i = 0; i < S; i++) jobs.Add((p, o, new[] { i }));
                    for (int i = 0; i < S; i++) for (int j = i + 1; j < S; j++) jobs.Add((p, o, new[] { i, j }));
                }
            Parallel.ForEach(Partitioner.Create(0, jobs.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, range =>
                {
                    for (int ji = range.Item1; ji < range.Item2; ji++)
                    {
                        var (p, o, sub) = jobs[ji];
                        var bad = new bool[S];
                        foreach (var x in sub) bad[x] = true;
                        var keep = Enumerable.Range(0, n).Where(i => i % p != o && !bad[ct[i]]);
                        var (per, plain, len) = Solve(ct, keep, 2, 991 + ji);
                        if (per > -9)
                            results.Add((per, $"every {p}th (off {o})" + (sub.Length > 0 ? " + " + string.Join(",", sub.Select(x => names[x])) : ""), plain, len));
                    }
                });
            foreach (var r in results.OrderByDescending(x => x.per).Take(10))
                Console.WriteLine($"  {r.per,9:F4} {r.len,5} kept   {r.what,-30} {r.plain[..Math.Min(38, r.plain.Length)]}");
            best.AddRange(results.OrderByDescending(x => x.per).Take(20));
            Console.WriteLine();
        }

        Console.WriteLine("BEST OVERALL");
        foreach (var r in best.OrderByDescending(x => x.per).Take(6))
        {
            Console.WriteLine($"  {r.per:F4}  {r.len} letters kept  --  {r.what}");
            Console.WriteLine($"    {r.plain}");
        }
    }
}
