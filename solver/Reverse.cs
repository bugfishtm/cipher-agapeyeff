using System.Text;

// "He ran it backwards."
//
// Cipher Mysteries (2017) reports that d'Agapeyeff's double-transposition section
// carries out the operation in the DECRYPTION direction and that he appears to have
// believed the transform was self-inverse. His worked example on pp.50-51 confirms a
// related slip: he sorts SCHUVALOF as ACHLOSUVF, dropping F to the end, and numbers
// it 623781459 where the alphabet gives 724891563.
//
// If he enciphered by running the DECRYPTION transform, then recovering the plaintext
// means running the ENCRYPTION transform on the ciphertext: write the ciphertext
// across the rows of a w-wide grid and read the columns off in key order. Each
// column is then a CONTIGUOUS run of plaintext, so the interiors survive and a
// substitution solve works even before the column order is right.
//
// That makes the width detectable on its own, which the ordinary direction never is.
static class Reverse
{
    // block c = ct[c], ct[c+w], ct[c+2w], ...
    static int[][] StrideBlocks(int[] ct, int w)
    {
        int n = ct.Length;
        var blocks = new int[w][];
        for (int c = 0; c < w; c++)
        {
            int len = (n - c + w - 1) / w;
            var b = new int[len];
            for (int t = 0; t < len; t++) b[t] = ct[c + t * w];
            blocks[c] = b;
        }
        return blocks;
    }
    static int[] Concat(int[][] blocks, int[] order, int n)
    {
        var outp = new int[n];
        int k = 0;
        foreach (var c in order) foreach (var v in blocks[c]) outp[k++] = v;
        return outp;
    }

    public static void Attack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 40;
        int restarts = a.Length > 3 ? int.Parse(a[3]) : 40;
        string digits = a.Length > 4 ? a[4] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int n = ct.Length, S = names.Length;
        var fo0 = Joint.FreqOrder(ct, S);

        Console.WriteLine($"REVERSED DIRECTION  {n} units, {S} distinct");
        Console.WriteLine("The ciphertext is written across the rows and the columns read off in key order.");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");
        Console.WriteLine();
        Console.WriteLine($"{"w",3} {"natural order",14} {"best order",12}   plaintext");

        var results = Enumerable.Range(wlo, whi - wlo + 1).AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .Select(w =>
            {
                var blocks = StrideBlocks(ct, w);
                // --- 1. natural column order, just to see if the width itself shows
                var nat = Concat(blocks, Enumerable.Range(0, w).ToArray(), n);
                var (natScore, _) = Routes.MonoSolve(nat, S, restarts, 41 + w, fo0);

                // --- 2. hill-climb the block order against the 4-gram model directly.
                // moving one block changes only two junctions, so the surface is smooth.
                var order = Enumerable.Range(0, w).ToArray();
                var rnd = new Random(9001 + w);
                double best = natScore; string bestTxt = ""; int[] bestOrd = (int[])order.Clone();
                for (int trial = 0; trial < 12; trial++)
                {
                    if (trial > 0) order = Enumerable.Range(0, w).OrderBy(_ => rnd.Next()).ToArray();
                    var (sc0, tx0) = Routes.MonoSolve(Concat(blocks, order, n), S, 4, 77 + w * 13 + trial, fo0);
                    double cur = sc0; string curTxt = tx0;
                    bool imp = true;
                    while (imp)
                    {
                        imp = false;
                        for (int i = 0; i < w; i++)
                            for (int j = i + 1; j < w; j++)
                            {
                                (order[i], order[j]) = (order[j], order[i]);
                                var (sc, tx) = Routes.MonoSolve(Concat(blocks, order, n), S, 1, 5, fo0);
                                if (sc > cur) { cur = sc; curTxt = tx; imp = true; }
                                else (order[i], order[j]) = (order[j], order[i]);
                            }
                    }
                    if (cur > best) { best = cur; bestTxt = curTxt; bestOrd = (int[])order.Clone(); }
                }
                if (bestTxt == "") { var (s2, t2) = Routes.MonoSolve(nat, S, restarts, 3, fo0); bestTxt = t2; }
                return (w, natScore, best, bestTxt, bestOrd);
            })
            .OrderBy(x => x.w).ToList();

        foreach (var r in results)
            Console.WriteLine($"{r.w,3} {r.natScore,14:F4} {r.best,12:F4}   {r.bestTxt[..Math.Min(46, r.bestTxt.Length)]}");

        Console.WriteLine();
        Console.WriteLine("BEST");
        foreach (var r in results.OrderByDescending(x => x.best).Take(3))
        {
            Console.WriteLine($"  w={r.w}  {r.best:F4}  columns [{string.Join(",", r.bestOrd)}]");
            Console.WriteLine($"    {r.bestTxt}");
        }
    }
}
