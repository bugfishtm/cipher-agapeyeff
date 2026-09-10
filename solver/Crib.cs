using System.Collections.Concurrent;
using System.Text;

// CRIB DRAGGING.
//
// Every previous attack asked a statistical question: does this key make the text
// look like English? That question overfits on short spans and cannot survive a
// message that is only partly correct.
//
// A crib asks a structural one. Suppose THREE appears somewhere in the plaintext.
// Its letter-repetition pattern - T H R E E, with the last two equal and the rest
// distinct - must be reproduced exactly by the ciphertext units the crib lands on.
// That is checkable WITHOUT knowing the substitution at all, and it either holds or
// it does not. No score, no threshold, no overfitting.
//
// In a columnar transposition, plaintext letter i sits at row i/w of column i%w, and
// each column is a contiguous block of ciphertext. So a crib placed at plaintext
// position p pins one unit in each of several column blocks, at known row offsets.
// We search assignments of blocks to column positions by depth-first descent,
// rejecting the moment the pattern breaks - which is almost immediately, so the tree
// is tiny. Survivors get their column order completed by the usual dynamic program
// and their substitution completed by hill-climbing.
static class Crib
{
    public static readonly string[] DEFAULT_CRIBS = {
        // a challenge cipher plausibly talks about itself
        "CRYPTOGRAM","CIPHER","CIPHERS","READER","SKILL","MESSAGE","SECRET","ALPHABET",
        "DAGAPEYEFF","THISISTHE","THEREADER","TESTHISSKILL","INVITED","SOLUTION","ANSWER",
        // 1939, and the book's own worked examples
        "ENGLAND","LONDON","GERMANY","WAR","ATTACK","BRIGADE","DIVISION","HEADQUARTERS",
        "TOMORROW","REUNION","STATION","RAILWAY","ADVANCE","RESERVE",
        // ordinary high-value English
        "THETHREE","THATTHE","WHICHTHE","THEREFORE","BECAUSE","BETWEEN","LETTERS",
        "TRANSPOSITION","SUBSTITUTION","KEYWORD","SQUARE","COLUMN"
    };

    // pattern signature: THREE -> 0,1,2,3,3
    static int[] Pattern(string s)
    {
        var map = new Dictionary<char, int>();
        var o = new int[s.Length];
        foreach (var (c, i) in s.Select((c, i) => (c, i)))
        {
            if (!map.TryGetValue(c, out int v)) { v = map.Count; map[c] = v; }
            o[i] = v;
        }
        return o;
    }

    sealed class Placement
    {
        public int W, P;                 // width, plaintext start position
        public int[] Order;              // block sitting at each column position (-1 = free)
        public int[] Sigma;              // partial substitution, -1 = unset
        public string CribText;
    }

    // does block b, placed at column position c, stay consistent with the crib so far?
    static bool Consistent(int[][] blocks, int b, int c, int w, int p, string crib,
                           int[] letterOfUnit, int[] unitOfLetter)
    {
        for (int k = 0; k < crib.Length; k++)
        {
            if ((p + k) % w != c) continue;
            int row = (p + k) / w;
            if (row >= blocks[b].Length) return false;      // crib runs off the short column
            int u = blocks[b][row];
            int L = crib[k] - 'A';
            if (letterOfUnit[u] >= 0 && letterOfUnit[u] != L) return false;
            if (unitOfLetter[L] >= 0 && unitOfLetter[L] != u) return false;
            letterOfUnit[u] = L; unitOfLetter[L] = u;
        }
        return true;
    }

    // hill-climb the substitution but never move a letter the crib pinned
    static double PolishFree(int[] syms, int S, int[] sigma, bool[] used, bool[] pinned, int[] L)
    {
        int n = syms.Length;
        void Reb() { for (int i = 0; i < n; i++) L[i] = sigma[syms[i]]; }
        Reb();
        double cur = P.Score(L);
        bool imp = true;
        while (imp)
        {
            imp = false;
            for (int a2 = 0; a2 < S; a2++)
            {
                if (pinned[a2]) continue;
                for (int b2 = a2 + 1; b2 < S; b2++)
                {
                    if (pinned[b2]) continue;
                    (sigma[a2], sigma[b2]) = (sigma[b2], sigma[a2]);
                    Reb(); double v = P.Score(L);
                    if (v > cur) { cur = v; imp = true; }
                    else (sigma[a2], sigma[b2]) = (sigma[b2], sigma[a2]);
                }
                for (int nl = 0; nl < 26; nl++)
                {
                    if (used[nl]) continue;
                    int old = sigma[a2];
                    sigma[a2] = nl; used[old] = false; used[nl] = true;
                    Reb(); double v = P.Score(L);
                    if (v > cur) { cur = v; imp = true; }
                    else { sigma[a2] = old; used[nl] = false; used[old] = true; }
                }
            }
        }
        Reb();
        return cur / Math.Max(1, n - 3);
    }

    public static int RESTARTS = 12;
    public const int MAX_SOLUTIONS = 40;
    public static int DIAG_P = -1, DIAG_W = -1;
    public static string DIAG_CRIB = "";

    public static void Attack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 15;
        int minLen = a.Length > 3 ? int.Parse(a[3]) : 6;
        bool control = a.Length > 4 && a[4] == "control";
        if (a.Length > 6) RESTARTS = int.Parse(a[6]);
        if (control) { DIAG_CRIB = "BEINGAFRICAN"; DIAG_W = 11; }
        string digits = a.Length > 5 ? a[5] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        int[] ct; int S; string truth = null;
        if (control)
        {
            ct = Modes.MakeControl(11, 0, out truth, out S, out _);
            Console.WriteLine("CRIB CONTROL - a known cipher, width 11.");
            Console.WriteLine($"  plaintext begins: {truth[..60]}");
        }
        else
        {
            ct = P.CipherUnits(digits, out var names);
            S = names.Length;
            Console.WriteLine($"CRIB DRAGGING  {ct.Length} units, {S} distinct");
        }
        int n = ct.Length;
        var fo = Joint.FreqOrder(ct, S);

        var cribs = DEFAULT_CRIBS.Where(c => c.Length >= minLen).ToArray();
        if (control)
        {
            // cribs that genuinely occur in the control plaintext, so the test means something
            cribs = new[] { "SOUNDS", "THEDRUM", "HOLLOW", "VIBRATES", "AFRICAN", "DRUMHOLLOW", "BEINGAFRICAN" }
                    .Where(c => c.Length >= minLen && truth.Contains(c)).ToArray();
            Console.WriteLine("  control cribs (all verified present): " + string.Join(" ", cribs));
            DIAG_P = truth.IndexOf(DIAG_CRIB);
            Console.WriteLine($"  true position of {DIAG_CRIB} in the plaintext: {DIAG_P}");
        }
        Console.WriteLine($"{cribs.Length} cribs of {minLen}+ letters, widths {wlo}-{whi}");
        Console.WriteLine("A hit is a placement whose letter-repetition pattern the ciphertext reproduces exactly.");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");
        Console.WriteLine();

        var hits = new ConcurrentBag<(double per, string crib, int w, int p, string plain)>();
        long totalPlacements = 0, survived = 0;

        var jobs = new List<(int w, string crib)>();
        for (int w = wlo; w <= whi; w++) foreach (var c in cribs) jobs.Add((w, c));

        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, job =>
        {
            int w = job.w; string crib = job.crib;
            if (crib.Length > n) return;
            int q = n / w, r = n % w;
            var posLen = new int[w];
            for (int i = 0; i < w; i++) posLen[i] = i < r ? q + 1 : q;

            var patterns = new List<int>();
            for (int mask = 0; mask < (1 << w); mask++)
                if (System.Numerics.BitOperations.PopCount((uint)mask) == r) patterns.Add(mask);
            if (patterns.Count > 600) patterns = patterns.Take(600).ToList();

            var dpArr = w <= 15 ? new double[(1 << w) * w] : null;
            var parArr = w <= 15 ? new int[(1 << w) * w] : null;
            var sigma = new int[S]; var used = new bool[26]; var L = new int[n];
            long local = 0, localSurv = 0;

            foreach (int pat in patterns)
            {
                // split ciphertext into blocks
                var blocks = new int[w][]; int off = 0;
                for (int k = 0; k < w; k++)
                {
                    int len = ((pat >> k) & 1) == 1 ? q + 1 : q;
                    blocks[k] = new int[len];
                    Array.Copy(ct, off, blocks[k], 0, len); off += len;
                }

                for (int p = 0; p + crib.Length <= n; p++)
                {
                    local++;
                    // a crib that pins fewer than four column positions constrains almost nothing
                    if (Math.Min(crib.Length, w) < 4) continue;
                    // which column positions does the crib touch, in ascending order?
                    var touched = new SortedSet<int>();
                    for (int k = 0; k < crib.Length; k++) touched.Add((p + k) % w);
                    var tl = touched.ToArray();

                    var order = new int[w]; Array.Fill(order, -1);
                    var usedBlock = new bool[w];
                    var letterOfUnit = new int[S]; var unitOfLetter = new int[26];

                    var solutions = new List<(int[] ord, int[] lu)>();
                    void Descend(int ti)
                    {
                        if (solutions.Count >= MAX_SOLUTIONS) return;
                        if (ti == tl.Length)
                        { solutions.Add(((int[])order.Clone(), (int[])letterOfUnit.Clone())); return; }
                        int c = tl[ti];
                        for (int b = 0; b < w; b++)
                        {
                            if (usedBlock[b]) continue;
                            if (blocks[b].Length != posLen[c]) continue;
                            var lu = (int[])letterOfUnit.Clone();
                            var ul = (int[])unitOfLetter.Clone();
                            if (!Consistent(blocks, b, c, w, p, crib, letterOfUnit, unitOfLetter))
                            { Array.Copy(lu, letterOfUnit, S); Array.Copy(ul, unitOfLetter, 26); continue; }
                            usedBlock[b] = true; order[c] = b;
                            Descend(ti + 1);
                            usedBlock[b] = false; order[c] = -1;
                            Array.Copy(lu, letterOfUnit, S); Array.Copy(ul, unitOfLetter, 26);
                        }
                    }

                    Array.Fill(letterOfUnit, -1); Array.Fill(unitOfLetter, -1);
                    Descend(0);
                    if (DIAG_P >= 0 && p == DIAG_P && crib == DIAG_CRIB && w == DIAG_W && solutions.Count > 0)
                        Console.WriteLine($"  [diag] p={p} pattern=0x{pat:X}: {solutions.Count} consistent orders");
                    if (solutions.Count == 0) continue;
                    localSurv++;

                    foreach (var (solOrder, solLU) in solutions)
                    {
                    Array.Copy(solOrder, order, w);
                    Array.Copy(solLU, letterOfUnit, S);
                    Array.Clear(usedBlock, 0, w);
                    foreach (var b in order) if (b >= 0) usedBlock[b] = true;
                    Array.Clear(used);
                    for (int i = 0; i < S; i++) sigma[i] = -1;
                    for (int u = 0; u < S; u++)
                        if (letterOfUnit[u] >= 0) { sigma[u] = letterOfUnit[u]; used[letterOfUnit[u]] = true; }
                    int nextEng = 0;
                    foreach (int u in fo)
                        if (sigma[u] < 0)
                        {
                            while (nextEng < 26 && used["ETAOINSHRDLCUMWFGYPBVKJXQZ"[nextEng] - 'A']) nextEng++;
                            if (nextEng >= 26) { sigma[u] = 0; continue; }
                            int lt = "ETAOINSHRDLCUMWFGYPBVKJXQZ"[nextEng] - 'A';
                            sigma[u] = lt; used[lt] = true;
                        }
                    var free = Enumerable.Range(0, w).Where(b => !usedBlock[b]).ToList();
                    for (int c = 0, fi = 0; c < w; c++)
                        if (order[c] < 0)
                        {
                            while (fi < free.Count && blocks[free[fi]].Length != posLen[c]) fi++;
                            if (fi < free.Count) { order[c] = free[fi]; usedBlock[free[fi]] = true; free.RemoveAt(fi); fi = 0; }
                        }
                    if (order.Any(x => x < 0)) continue;

                    // assemble, then polish the substitution against the 4-gram model while
                    // holding every crib-derived letter fixed
                    int rows = blocks.Max(b => b.Length);
                    var syms = new int[n]; int idx = 0;
                    for (int row = 0; row < rows; row++)
                        for (int c = 0; c < w; c++)
                        { var b = blocks[order[c]]; if (row < b.Length) syms[idx++] = b[row]; }

                    var pinned = new bool[S];
                    for (int u = 0; u < S; u++) pinned[u] = letterOfUnit[u] >= 0;

                    // the pinned letters are fixed; the rest need real restarts, not one greedy pass
                    double bestSc = double.NegativeInfinity; int[] bestSig = null;
                    var rnd2 = new Random(p * 7919 + w * 104729 + crib.Length);
                    var freeUnits = Enumerable.Range(0, S).Where(u => !pinned[u]).ToArray();
                    for (int t = 0; t < RESTARTS; t++)
                    {
                        Array.Clear(used);
                        for (int u = 0; u < S; u++)
                            if (pinned[u]) { sigma[u] = letterOfUnit[u]; used[letterOfUnit[u]] = true; }
                        var pool = Enumerable.Range(0, 26).Where(x => !used[x])
                                             .OrderBy(_ => rnd2.Next()).ToArray();
                        for (int i = 0; i < freeUnits.Length && i < pool.Length; i++)
                        { sigma[freeUnits[i]] = pool[i]; used[pool[i]] = true; }
                        double v = PolishFree(syms, S, sigma, used, pinned, L);
                        if (v > bestSc) { bestSc = v; bestSig = (int[])sigma.Clone(); }
                    }
                    if (bestSig == null) continue;
                    var sb = new StringBuilder(n);
                    for (int i = 0; i < n; i++) sb.Append((char)('A' + bestSig[syms[i]]));
                    hits.Add((bestSc, crib, w, p, sb.ToString()));
                    }
                }
            }
            Interlocked.Add(ref totalPlacements, local);
            Interlocked.Add(ref survived, localSurv);
        });

        Console.WriteLine($"placements tested : {totalPlacements:N0}");
        Console.WriteLine($"pattern-consistent: {survived:N0}   ({(survived == 0 ? 0 : 100.0 * survived / totalPlacements):F4}% survive)");
        Console.WriteLine();

        var best = hits.OrderByDescending(h => h.per).Take(15).ToList();
        if (best.Count == 0) { Console.WriteLine("no placement survived the pattern test at all."); return; }
        Console.WriteLine($"{"score",8} {"crib",-14} {"w",3} {"pos",4}   plaintext");
        foreach (var h in best)
        {
            string mark = "";
            if (control && truth != null) mark = h.plain[..Math.Min(40, h.plain.Length)] == truth[..40] ? "  <== EXACT" : "";
            Console.WriteLine($"{h.per,8:F4} {h.crib,-14} {h.w,3} {h.p,4}   {h.plain[..Math.Min(46, h.plain.Length)]}{mark}");
        }
        if (control && truth != null)
        {
            var win = hits.OrderByDescending(h => h.per).First();
            int m = 0; for (int i = 0; i < 196; i++) if (win.plain[i] == truth[i]) m++;
            Console.WriteLine();
            Console.WriteLine($"CONTROL RESULT: {m}/196 letters correct  {(m > 170 ? "SOLVED" : "FAILED")}");
        }
    }
}
