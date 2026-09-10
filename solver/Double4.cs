using System.Collections.Concurrent;

// Priority 2: double transposition in ALL FOUR direction combinations.
//
// The 2017 Cipher Mysteries finding is that d'Agapeyeff carried out his double
// transposition in the decryption direction and appears to have thought the
// transform was self-inverse. The double transposition already run used only the
// forward reading for both stages. For an INCOMPLETE rectangle - which 196 units
// is at most widths - apply and undo are genuinely different permutations, so
// three of the four combinations have never been searched.
static class Double4
{
    static List<int[]> Perms(int w)
    {
        var res = new List<int[]>();
        var cur = new int[w]; var used = new bool[w];
        void Rec(int d)
        {
            if (d == w) { res.Add((int[])cur.Clone()); return; }
            for (int c = 0; c < w; c++) { if (used[c]) continue; used[c] = true; cur[d] = c; Rec(d + 1); used[c] = false; }
        }
        Rec(0); return res;
    }

    public static void Attack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 8;
        string digits = a.Length > 3 ? a[3] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length, n = ct.Length;
        Console.WriteLine($"DOUBLE TRANSPOSITION, ALL FOUR DIRECTIONS  {n} units, {S} distinct");
        Console.WriteLine("d1/d2: U = undo (read columns into rows), A = apply (rows into columns)");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");
        Console.WriteLine();
        Console.WriteLine($"{"dir",4} {"w1",3} {"w2",3} {"keys",14} {"best/letter",12}   plaintext");

        string[] dirNames = { "UU", "UA", "AU", "AA" };
        for (int d = 0; d < 4; d++)
        {
            bool firstUndo = d < 2, secondUndo = (d % 2) == 0;
            for (int w1 = wlo; w1 <= whi; w1++)
                for (int w2 = wlo; w2 <= whi; w2++)
                {
                    var p1 = Perms(w1); var p2 = Perms(w2);
                    long total = (long)p1.Count * p2.Count;
                    if (total > 30_000_000) continue;
                    object gate = new(); double best = double.NegativeInfinity; string bt = "";
                    Parallel.ForEach(Partitioner.Create(0, p1.Count),
                        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, range =>
                        {
                            var t1 = new int[n]; var t2 = new int[n];
                            var s1 = new int[w1]; var l1 = new int[w1];
                            var s2 = new int[w2]; var l2 = new int[w2];
                            double loc = double.NegativeInfinity; string lt = "";
                            for (int i = range.Item1; i < range.Item2; i++)
                                for (int j = 0; j < p2.Count; j++)
                                {
                                    // undo the second stage first
                                    if (secondUndo) P.Undo(ct, p2[j], s2, l2, t1);
                                    else P.Apply(ct, p2[j], s2, l2, t1);
                                    if (firstUndo) P.Undo(t1, p1[i], s1, l1, t2);
                                    else P.Apply(t1, p1[i], s1, l1, t2);
                                    var fo = Joint.FreqOrder(t2, S);
                                    var (per, txt) = Routes.MonoSolve(t2, S, 1, 13 + i * 31 + j, fo);
                                    if (per > loc) { loc = per; lt = txt; }
                                }
                            lock (gate) { if (loc > best) { best = loc; bt = lt; } }
                        });
                    Console.WriteLine($"{dirNames[d],4} {w1,3} {w2,3} {total,14:N0} {best,12:F4}   {bt[..Math.Min(44, bt.Length)]}");
                }
        }
    }
}
