using System.Collections.Concurrent;

// Try real words as the transposition key, numbered by alphabetical rank exactly
// as the book does on page 117. The candidate list is the author's own vocabulary
// -- every word of three letters or more that appears anywhere in Codes and Ciphers --
// plus names, dates and phrases connected to the book.
static class Words
{
    public static int[] KeyOrder(string key)
    {
        var L = key.ToUpperInvariant().Where(char.IsLetter).ToArray();
        int w = L.Length;
        var idx = Enumerable.Range(0, w).ToArray();
        Array.Sort(idx, (x, y) => L[x] != L[y] ? L[x].CompareTo(L[y]) : x.CompareTo(y));
        return idx;                       // idx[k] = column read k-th
    }

    public static void Attack(string[] a)
    {
        string path = a.Length > 1 ? a[1] : "words.txt";
        int maxw = a.Length > 2 ? int.Parse(a[2]) : 30;
        string digits = a.Length > 3 ? a[3] : P.PRINTED.Replace(" ", "").Substring(0, 392);
        if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "words.txt");
        var keys = File.ReadAllLines(path).Where(x => x.Trim().Length >= 2).Select(x => x.Trim()).ToArray();

        var ctUnits = P.CipherUnits(digits, out var names);
        int S = names.Length, n = ctUnits.Length;
        var D = digits.Select(c => c - '0').ToArray();

        Console.WriteLine($"WORD-KEY SWEEP  {keys.Length:N0} candidate keys x 4 readings = {keys.Length * 4:N0} trials");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");

        var results = new ConcurrentBag<(double per, string key, string how, string plain)>();

        Parallel.ForEach(Partitioner.Create(0, keys.Length),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, range =>
            {
                for (int ki = range.Item1; ki < range.Item2; ki++)
                {
                    var key = keys[ki];
                    var ord = KeyOrder(key);
                    int w = ord.Length;
                    if (w < 2 || w > maxw) continue;

                    // --- whole units, both directions
                    {
                        var st = new int[w]; var ln = new int[w]; var mid = new int[n];
                        for (int dir = 0; dir < 2; dir++)
                        {
                            if (dir == 0) P.Undo(ctUnits, ord, st, ln, mid);
                            else P.Apply(ctUnits, ord, st, ln, mid);
                            var fo = Joint.FreqOrder(mid, S);
                            var (per, plain) = Routes.MonoSolve(mid, S, 2, 7 + ki, fo);
                            results.Add((per, key, dir == 0 ? "units, undo" : "units, apply", plain));
                        }
                    }
                    // --- single digits, both directions
                    {
                        var st = new int[w]; var ln = new int[w]; var mid = new int[D.Length];
                        for (int dir = 0; dir < 2; dir++)
                        {
                            if (dir == 0) P.Undo(D, ord, st, ln, mid);
                            else P.Apply(D, ord, st, ln, mid);
                            int m = mid.Length / 2;
                            var syms = new int[m];
                            var map = new int[100]; for (int i = 0; i < 100; i++) map[i] = -1;
                            int s2 = 0; bool ok = true;
                            for (int i = 0; i < m; i++)
                            {
                                int u = mid[2 * i] * 10 + mid[2 * i + 1];
                                if (map[u] < 0) { if (s2 >= 26) { ok = false; break; } map[u] = s2++; }
                                syms[i] = map[u];
                            }
                            if (!ok) continue;
                            var fo = Joint.FreqOrder(syms, s2);
                            var (per, plain) = Routes.MonoSolve(syms, s2, 2, 9 + ki, fo);
                            results.Add((per, key, dir == 0 ? "digits, undo" : "digits, apply", plain));
                        }
                    }
                }
            });

        var top = results.OrderByDescending(r => r.per).Take(12).ToList();
        Console.WriteLine($"{"per letter",11}  {"key",-22} {"reading",-14} plaintext");
        foreach (var r in top)
            Console.WriteLine($"{r.per,11:F4}  {r.key,-22} {r.how,-14} {r.plain[..Math.Min(46, r.plain.Length)]}");
    }
}
