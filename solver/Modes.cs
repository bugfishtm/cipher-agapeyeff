static class Modes
{
    public static int[] MakeControl(int w, int seedOff, out string truth, out int S, out int[] truePerm)
    {
        var rnd = new Random(20250823 + seedOff);
        string pt = P.Corpus.Substring(60000, 196).Replace('J', 'I');
        truth = pt;
        var sq = "MANCHESTRBDFGIKLOPQUVWXYZ";
        var cell = new Dictionary<char, int>();
        for (int i = 0; i < 25; i++) cell[sq[i]] = i;
        var syms = pt.Select(c => cell.ContainsKey(c) ? cell[c] : cell['X']).ToArray();
        var perm = Enumerable.Range(0, w).OrderBy(_ => rnd.Next()).ToArray();
        truePerm = perm;
        var ctArr = new int[syms.Length];
        var st = new int[w]; var ln = new int[w];
        P.Apply(syms, perm, st, ln, ctArr);
        var uniq = ctArr.Distinct().OrderBy(x => x).ToArray();
        var map = uniq.Select((u, i) => (u, i)).ToDictionary(p => p.u, p => p.i);
        S = uniq.Length;
        return ctArr.Select(x => map[x]).ToArray();
    }

    public static void DpControl(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 11;
        int whi = a.Length > 2 ? int.Parse(a[2]) : wlo;
        int seeds = a.Length > 3 ? int.Parse(a[3]) : 6;
        Console.WriteLine("DP CONTROL  -- English through a keyword square, then a columnar transposition");
        Console.WriteLine($"{"true w",7} {"tried w",8} {"per letter",11} {"correct",9}   plaintext");
        for (int tw = wlo; tw <= whi; tw++)
        {
            var ct = MakeControl(tw, 0, out var truth, out int S, out var tp);
            var fo = Joint.FreqOrder(ct, S);
            var hit = Dp.Attack(ct, S, tw, seeds, 99, fo);
            int m = 0; for (int i = 0; i < 196; i++) if (hit.Plain[i] == truth[i]) m++;
            Console.WriteLine($"{tw,7} {tw,8} {hit.Per,11:F4} {m + "/196",9}   {hit.Plain[..52]}");
        }
    }

    public static void DpSweep(string[] a)
    {
        int trueW = a.Length > 1 ? int.Parse(a[1]) : 11;
        int wlo = a.Length > 2 ? int.Parse(a[2]) : 2;
        int whi = a.Length > 3 ? int.Parse(a[3]) : 15;
        int seeds = a.Length > 4 ? int.Parse(a[4]) : 6;
        var ct = MakeControl(trueW, 0, out var truth, out int S, out var tp);
        var fo = Joint.FreqOrder(ct, S);
        Console.WriteLine($"DP SWEEP over widths, control built with true width {trueW}");
        Console.WriteLine($"{"w",3} {"per letter",11} {"correct",9}   plaintext");
        var jobs = Enumerable.Range(wlo, whi - wlo + 1).ToArray();
        var res = jobs.AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount)
                      .Select(w => (w, hit: Dp.Attack(ct, S, w, seeds, 99 + w, fo)))
                      .OrderBy(x => x.w).ToList();
        foreach (var (w, hit) in res)
        {
            if (hit == null) { Console.WriteLine($"{w,3}  (none)"); continue; }
            int m = 0; for (int i = 0; i < 196; i++) if (hit.Plain[i] == truth[i]) m++;
            Console.WriteLine($"{w,3} {hit.Per,11:F4} {m + "/196",9}   {hit.Plain[..52]}");
        }
    }

    public static void DpAttack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 15;
        int seeds = a.Length > 3 ? int.Parse(a[3]) : 8;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length;
        var fo = Joint.FreqOrder(ct, S);
        Console.WriteLine($"DP ATTACK on {ct.Length} units, {S} distinct: {string.Join(" ", names)}");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89, random -2.36");
        Console.WriteLine($"{"w",3} {"per letter",11}   plaintext");
        var res = Enumerable.Range(wlo, whi - wlo + 1).AsParallel()
                      .WithDegreeOfParallelism(Environment.ProcessorCount)
                      .Select(w => (w, hit: Dp.Attack(ct, S, w, seeds, 4242 + w, fo)))
                      .OrderBy(x => x.w).ToList();
        foreach (var (w, hit) in res)
        {
            if (hit == null) { Console.WriteLine($"{w,3}  (none)"); continue; }
            Console.WriteLine($"{w,3} {hit.Per,11:F4}   {hit.Plain[..60]}");
        }
        Console.WriteLine();
        foreach (var (w, hit) in res.Where(x => x.hit != null).OrderByDescending(x => x.hit.Per).Take(3))
        {
            Console.WriteLine($"w={w}  {hit.Per:F4}  columns [{string.Join(",", hit.BlockOrder)}]  long blocks [{string.Join(",", hit.LongSet)}]");
            Console.WriteLine($"  {hit.Plain}");
        }
    }

    public static void Irregular(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 15;
        int seeds = a.Length > 3 ? int.Parse(a[3]) : 60;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);
        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length;
        var fo = Joint.FreqOrder(ct, S);
        Console.WriteLine($"IRREGULAR COLUMNAR  -- the short cells put in the wrong columns");
        Console.WriteLine($"{ct.Length} units, {S} distinct.  reference: real English -0.74, shuffled -1.89");
        Console.WriteLine($"{"w",3} {"per letter",11}   plaintext");
        var res = Enumerable.Range(wlo, whi - wlo + 1).AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .Select(w => (w, hit: Dp.Attack(ct, S, w, seeds, 555 + w, fo, true)))
                    .OrderBy(x => x.w).ToList();
        foreach (var (w, hit) in res)
            Console.WriteLine(hit == null ? $"{w,3}  (none)" : $"{w,3} {hit.Per,11:F4}   {hit.Plain[..60]}");
        Console.WriteLine();
        foreach (var (w, hit) in res.Where(x => x.hit != null).OrderByDescending(x => x.hit.Per).Take(2))
        {
            Console.WriteLine($"w={w}  {hit.Per:F4}  columns [{string.Join(",", hit.BlockOrder)}]");
            Console.WriteLine($"  {hit.Plain}");
        }
    }
}
