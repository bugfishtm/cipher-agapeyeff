using System.Text;

// Priority 1: score only the OPENING of the candidate plaintext.
//
// If d'Agapeyeff dropped or doubled a letter part-way through filling his
// transposition rectangle, then everything after the slip is scrambled by a
// transposition that never existed, and NO key reads the whole message. But the
// correct key still reads the letters written before the slip. Every search so far
// scored the whole text, so a correct-then-broken solution was thrown away as noise.
//
// Here the four-gram score is taken over the first Head letters only. Sweep Head
// and watch for a width whose score rises sharply at small Head and then decays -
// that decay point is where the encipherer's hand slipped.
static class Head
{
    public static void Attack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 15;
        int seeds = a.Length > 3 ? int.Parse(a[3]) : 250;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length;
        var fo = Joint.FreqOrder(ct, S);

        int[] heads = { 24, 32, 40, 50, 64, 80, 100, 130, 196 };
        Console.WriteLine($"HEAD-SCORED SWEEP  {ct.Length} units, {S} distinct");
        Console.WriteLine("Only the first N letters are scored. A genuine partial solution shows as a high");
        Console.WriteLine("score at small N that decays as N grows past the point where the hand slipped.");
        Console.WriteLine();
        Console.WriteLine("Reference, real English scored over its own first N letters:");
        var rnd = new Random(5);
        foreach (var h in heads)
        {
            double sum = 0; int c = 0;
            for (int i = 0; i < 300; i++)
            {
                int p = rnd.Next(P.Corpus.Length - 260);
                var t = P.Corpus.Substring(p, h);
                var L = t.Select(ch => ch - 'A').ToArray();
                sum += P.Score(L) / Math.Max(1, L.Length - 3); c++;
            }
            Console.Write($"  N={h,3}: {sum / c,7:F3}");
        }
        Console.WriteLine();
        Console.WriteLine();

        Console.Write($"{"w",3}");
        foreach (var h in heads) Console.Write($" {("N=" + h),9}");
        Console.WriteLine("   best opening at N=40");

        var rows = new List<(int w, double[] sc, string open)>();
        foreach (int w in Enumerable.Range(wlo, whi - wlo + 1))
        {
            var sc = new double[heads.Length];
            string open40 = "";
            var results = heads.Select((h, hi) => (h, hi)).AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Select(x =>
                {
                    P.HeadLimit = x.h;
                    var hit = Dp.Attack(ct, S, w, Math.Max(20, seeds / 4), 606 + w * 31 + x.h, fo);
                    P.HeadLimit = 0;
                    return (x.hi, per: hit?.Per ?? -9, plain: hit?.Plain ?? "");
                }).ToList();
            foreach (var r in results)
            {
                sc[r.hi] = r.per;
                if (heads[r.hi] == 40) open40 = r.plain;
            }
            rows.Add((w, sc, open40));
            Console.Write($"{w,3}");
            foreach (var v in sc) Console.Write($" {v,9:F3}");
            Console.WriteLine($"   {open40[..Math.Min(40, open40.Length)]}");
        }

        Console.WriteLine();
        Console.WriteLine("STRONGEST OPENINGS (by score at N=40)");
        int idx40 = Array.IndexOf(heads, 40);
        foreach (var r in rows.OrderByDescending(x => x.sc[idx40]).Take(5))
            Console.WriteLine($"  w={r.w,2}  {r.sc[idx40]:F3}   {r.open[..Math.Min(70, r.open.Length)]}");
    }
}
