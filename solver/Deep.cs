// One width, many substitution seeds, spread across every core.
// Needed because widths with few long-column patterns (w = 14 has exactly one)
// get far fewer effective restarts than widths with many, and the control shows
// 250 seeds is not enough past width 12.
static class Deep
{
    public static void Run(string[] a)
    {
        bool control = a.Length > 1 && a[1] == "control";
        int w = a.Length > 2 ? int.Parse(a[2]) : 14;
        int seedsPerThread = a.Length > 3 ? int.Parse(a[3]) : 2000;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        int[] ct; int S; string truth = null;
        if (control)
        {
            ct = Modes.MakeControl(w, 0, out truth, out S, out _);
        }
        else
        {
            ct = P.CipherUnits(digits, out var names);
            S = names.Length;
        }
        var fo = Joint.FreqOrder(ct, S);
        int T = Environment.ProcessorCount;
        Console.WriteLine($"{(control ? "DEEP CONTROL" : "DEEP ATTACK")}  width {w}, {seedsPerThread:N0} seeds x {T} threads = {(long)seedsPerThread * T:N0}");

        var best = Enumerable.Range(0, T).AsParallel().WithDegreeOfParallelism(T)
            .Select(k => Dp.Attack(ct, S, w, seedsPerThread, 100003 * (k + 1) + w, fo))
            .Where(x => x != null)
            .OrderByDescending(x => x.Per).FirstOrDefault();

        if (best == null) { Console.WriteLine("  no valid arrangement"); return; }
        Console.Write($"  w={w}  {best.Per:F4}");
        if (control)
        {
            int m = 0;
            for (int i = 0; i < 196; i++) if (best.Plain[i] == truth[i]) m++;
            Console.Write($"  {m}/196  {(m > 170 ? "SOLVED" : "FAILED")}");
        }
        Console.WriteLine();
        Console.WriteLine($"  columns [{string.Join(",", best.BlockOrder)}]  long blocks [{string.Join(",", best.LongSet)}]");
        Console.WriteLine($"  {best.Plain}");
    }
}
