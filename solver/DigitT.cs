using System.Collections.Concurrent;
using System.Text;

// Transposition applied to the DIGIT stream, not to whole units.
// Such a key only survives if it leaves the 6789 / 12345 alternation intact,
// which constrains the column order sharply. Every surviving key is enumerated,
// the transposition undone, the digits re-paired, and the result solved as a
// monoalphabetic substitution.
static class DigitT
{
    public static List<int[]> ValidOrders(int n, int w, long cap)
    {
        var res = new List<int[]>();
        if (w % 2 == 0) return res;                    // every column single-parity
        int q = n / w, r = n % w;
        var len = new int[w];
        for (int c = 0; c < w; c++) len[c] = c < r ? q + 1 : q;

        var order = new int[w];
        var used = new bool[w];
        void Rec(int depth, int offPar)
        {
            if (res.Count >= cap) return;
            if (depth == w) { res.Add((int[])order.Clone()); return; }
            for (int c = 0; c < w; c++)
            {
                if (used[c]) continue;
                if ((c & 1) != offPar) continue;
                used[c] = true; order[depth] = c;
                Rec(depth + 1, (offPar + len[c]) & 1);
                used[c] = false;
            }
        }
        Rec(0, 0);
        return res;
    }

    public static void Attack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 3;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 13;
        int deep = a.Length > 3 ? int.Parse(a[3]) : 40;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var D = digits.Select(c => c - '0').ToArray();
        int n = D.Length;
        Console.WriteLine($"DIGIT-LEVEL TRANSPOSITION  {n} digits");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");
        Console.WriteLine($"{"w",3} {"keys",12} {"best/letter",12}   plaintext");

        for (int w = wlo; w <= whi; w++)
        {
            var orders = ValidOrders(n, w, 4_000_000);
            if (orders.Count == 0) { Console.WriteLine($"{w,3} {0,12}   impossible - no key preserves the alternation"); continue; }

            object gate = new();
            double best = double.NegativeInfinity;
            string bestTxt = "";
            int[] bestOrd = null;

            Parallel.ForEach(Partitioner.Create(0, orders.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                range =>
                {
                    var start = new int[w]; var len = new int[w];
                    var mid = new int[n];
                    double loc = double.NegativeInfinity; string locTxt = ""; int[] locOrd = null;
                    for (int oi = range.Item1; oi < range.Item2; oi++)
                    {
                        var perm = orders[oi];
                        P.Undo(D, perm, start, len, mid);
                        // re-pair and index
                        int m = n / 2;
                        var syms = new int[m];
                        var map = new int[100];
                        for (int i = 0; i < 100; i++) map[i] = -1;
                        int S = 0;
                        for (int i = 0; i < m; i++)
                        {
                            int u = mid[2 * i] * 10 + mid[2 * i + 1];
                            if (map[u] < 0) map[u] = S++;
                            syms[i] = map[u];
                        }
                        if (S > 26) continue;
                        var fo = Joint.FreqOrder(syms, S);
                        var (per, plain) = Routes.MonoSolve(syms, S, 2, 17 + oi, fo);
                        if (per > loc) { loc = per; locTxt = plain; locOrd = perm; }
                    }
                    lock (gate)
                    {
                        if (loc > best) { best = loc; bestTxt = locTxt; bestOrd = locOrd; }
                    }
                });

            // deep re-solve of the winner
            if (bestOrd != null)
            {
                var start = new int[w]; var len = new int[w]; var mid = new int[n];
                P.Undo(D, bestOrd, start, len, mid);
                int m = n / 2;
                var syms = new int[m];
                var map = new int[100]; for (int i = 0; i < 100; i++) map[i] = -1;
                int S = 0;
                for (int i = 0; i < m; i++)
                {
                    int u = mid[2 * i] * 10 + mid[2 * i + 1];
                    if (map[u] < 0) map[u] = S++;
                    syms[i] = map[u];
                }
                var fo = Joint.FreqOrder(syms, S);
                var (per, plain) = Routes.MonoSolve(syms, S, deep, 99, fo);
                if (per > best) { best = per; bestTxt = plain; }
            }
            Console.WriteLine($"{w,3} {orders.Count,12:N0} {best,12:F4}   {bestTxt[..Math.Min(52, bestTxt.Length)]}");
        }
    }
}
