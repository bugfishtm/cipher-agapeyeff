using System.Collections.Concurrent;

// Double columnar transposition of whole units.
// Two cases are within reach by exhaustion:
//   same   - one key applied twice, w = 2..10  (w! orders)
//   pair   - two different keys, both narrow   (w1! * w2! orders)
static class Double
{
    static List<int[]> Perms(int w)
    {
        var res = new List<int[]>();
        var cur = new int[w];
        var used = new bool[w];
        void Rec(int d)
        {
            if (d == w) { res.Add((int[])cur.Clone()); return; }
            for (int c = 0; c < w; c++)
            {
                if (used[c]) continue;
                used[c] = true; cur[d] = c; Rec(d + 1); used[c] = false;
            }
        }
        Rec(0);
        return res;
    }

    static double Try(int[] ct, int S, int[] k1, int[] k2, int[] t1, int[] t2,
                      int[] s1, int[] l1, int[] s2, int[] l2, int restarts, int seed, out string plain)
    {
        P.Undo(ct, k2, s2, l2, t1);
        P.Undo(t1, k1, s1, l1, t2);
        var fo = Joint.FreqOrder(t2, S);
        var (per, txt) = Routes.MonoSolve(t2, S, restarts, seed, fo);
        plain = txt;
        return per;
    }

    public static void Attack(string[] a)
    {
        string mode = a.Length > 1 ? a[1] : "same";
        int wlo = a.Length > 2 ? int.Parse(a[2]) : 2;
        int whi = a.Length > 3 ? int.Parse(a[3]) : 9;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length, n = ct.Length;
        Console.WriteLine($"DOUBLE TRANSPOSITION ({mode})  {n} units, {S} distinct");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");

        if (mode == "same")
        {
            Console.WriteLine($"{"w",3} {"keys",12} {"best/letter",12}   plaintext");
            for (int w = wlo; w <= whi; w++)
            {
                var perms = Perms(w);
                object gate = new(); double best = double.NegativeInfinity; string bt = ""; int[] bk = null;
                Parallel.ForEach(Partitioner.Create(0, perms.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, range =>
                    {
                        var t1 = new int[n]; var t2 = new int[n];
                        var s1 = new int[w]; var l1 = new int[w]; var s2 = new int[w]; var l2 = new int[w];
                        double loc = double.NegativeInfinity; string lt = ""; int[] lk = null;
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            var k = perms[i];
                            double v = Try(ct, S, k, k, t1, t2, s1, l1, s2, l2, 1, 11 + i, out var txt);
                            if (v > loc) { loc = v; lt = txt; lk = k; }
                        }
                        lock (gate) { if (loc > best) { best = loc; bt = lt; bk = lk; } }
                    });
                if (bk != null)
                {
                    var t1 = new int[n]; var t2 = new int[n];
                    var s1 = new int[w]; var l1 = new int[w]; var s2 = new int[w]; var l2 = new int[w];
                    double v = Try(ct, S, bk, bk, t1, t2, s1, l1, s2, l2, 60, 77, out var txt);
                    if (v > best) { best = v; bt = txt; }
                }
                Console.WriteLine($"{w,3} {perms.Count,12:N0} {best,12:F4}   {bt[..Math.Min(52, bt.Length)]}");
            }
        }
        else
        {
            Console.WriteLine($"{"w1",3} {"w2",3} {"keys",14} {"best/letter",12}   plaintext");
            for (int w1 = wlo; w1 <= whi; w1++)
                for (int w2 = wlo; w2 <= whi; w2++)
                {
                    var p1 = Perms(w1); var p2 = Perms(w2);
                    long total = (long)p1.Count * p2.Count;
                    object gate = new(); double best = double.NegativeInfinity; string bt = "";
                    int[] bk1 = null, bk2 = null;
                    Parallel.ForEach(Partitioner.Create(0, p1.Count),
                        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, range =>
                        {
                            var t1 = new int[n]; var t2 = new int[n];
                            var s1 = new int[w1]; var l1 = new int[w1]; var s2 = new int[w2]; var l2 = new int[w2];
                            double loc = double.NegativeInfinity; string lt = ""; int[] la = null, lb = null;
                            for (int i = range.Item1; i < range.Item2; i++)
                                for (int j = 0; j < p2.Count; j++)
                                {
                                    double v = Try(ct, S, p1[i], p2[j], t1, t2, s1, l1, s2, l2, 1, 13 + i * 31 + j, out var txt);
                                    if (v > loc) { loc = v; lt = txt; la = p1[i]; lb = p2[j]; }
                                }
                            lock (gate) { if (loc > best) { best = loc; bt = lt; bk1 = la; bk2 = lb; } }
                        });
                    if (bk1 != null)
                    {
                        var t1 = new int[n]; var t2 = new int[n];
                        var s1 = new int[w1]; var l1 = new int[w1]; var s2 = new int[w2]; var l2 = new int[w2];
                        double v = Try(ct, S, bk1, bk2, t1, t2, s1, l1, s2, l2, 60, 79, out var txt);
                        if (v > best) { best = v; bt = txt; }
                    }
                    Console.WriteLine($"{w1,3} {w2,3} {total,14:N0} {best,12:F4}   {bt[..Math.Min(48, bt.Length)]}");
                }
        }
    }
}
