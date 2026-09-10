using System.Text;

// Sweep of unkeyed geometric transpositions. For each candidate reading order the
// units are put back in that order and then solved as a plain monoalphabetic
// substitution, which is reliable at this length.
static class Routes
{
    static readonly int[] EngRank = "ETAOINSHRDLCUMWFGYPBVKJXQZ".Select(c => c - 'A').ToArray();

    public static (double per, string plain) MonoSolve(int[] syms, int S, int restarts, int seed, int[] freqOrder)
    {
        int n = syms.Length;
        var rnd = new Random(seed);
        var sigma = new int[S];
        var used = new bool[26];
        var L = new int[n];
        double best = double.NegativeInfinity;
        string bestTxt = "";

        for (int r = 0; r < restarts; r++)
        {
            Array.Clear(used);
            if (r == 0 && freqOrder != null)
                for (int i = 0; i < S; i++) { sigma[freqOrder[i]] = EngRank[i]; used[EngRank[i]] = true; }
            else
            {
                var pool = Enumerable.Range(0, 26).OrderBy(_ => rnd.Next()).ToArray();
                for (int i = 0; i < S; i++) { sigma[i] = pool[i]; used[pool[i]] = true; }
            }
            for (int i = 0; i < n; i++) L[i] = sigma[syms[i]];
            double cur = P.Score(L);
            bool imp = true;
            while (imp)
            {
                imp = false;
                for (int a = 0; a < S; a++)
                    for (int b = a + 1; b < S; b++)
                    {
                        (sigma[a], sigma[b]) = (sigma[b], sigma[a]);
                        for (int i = 0; i < n; i++) L[i] = sigma[syms[i]];
                        double v = P.Score(L);
                        if (v > cur) { cur = v; imp = true; }
                        else (sigma[a], sigma[b]) = (sigma[b], sigma[a]);
                    }
                for (int a = 0; a < S; a++)
                    for (int nl = 0; nl < 26; nl++)
                    {
                        if (used[nl]) continue;
                        int old = sigma[a];
                        sigma[a] = nl; used[old] = false; used[nl] = true;
                        for (int i = 0; i < n; i++) L[i] = sigma[syms[i]];
                        double v = P.Score(L);
                        if (v > cur) { cur = v; imp = true; }
                        else { sigma[a] = old; used[nl] = false; used[old] = true; }
                    }
            }
            if (cur > best)
            {
                best = cur;
                for (int i = 0; i < n; i++) L[i] = sigma[syms[i]];
                var sb = new StringBuilder(n);
                for (int i = 0; i < n; i++) sb.Append((char)('A' + L[i]));
                bestTxt = sb.ToString();
            }
        }
        return (best / (n - 3), bestTxt);
    }

    // every candidate returns ctIndex[i] = where plaintext unit i sits in the ciphertext
    public static List<(string name, int[] map)> Candidates(int n)
    {
        var outp = new List<(string, int[])>();

        // ---- no transposition at all, for reference
        var id = new int[n]; for (int i = 0; i < n; i++) id[i] = i;
        outp.Add(("no transposition", id));

        // ---- columnar, unkeyed, and its boustrophedon variants
        for (int w = 2; w <= 40; w++)
        {
            int q = n / w, r = n % w;
            var len = new int[w]; for (int c = 0; c < w; c++) len[c] = c < r ? q + 1 : q;
            var st = new int[w]; for (int c = 1; c < w; c++) st[c] = st[c - 1] + len[c - 1];

            var m1 = new int[n]; var m2 = new int[n]; var m3 = new int[n];
            for (int i = 0; i < n; i++)
            {
                int c = i % w, row = i / w;
                m1[i] = st[c] + row;                                   // straight down every column
                m2[i] = st[c] + (c % 2 == 0 ? row : len[c] - 1 - row); // alternate down / up
                m3[i] = 0;
            }
            outp.Add(($"columnar w={w}", m1));
            outp.Add(($"boustrophedon columns w={w}", m2));
            if (r == 0)
            {
                for (int i = 0; i < n; i++) { int c = i % w, row = i / w; m3[i] = st[w - 1 - c] + row; }
                outp.Add(($"columns right-to-left w={w}", m3));
            }
        }

        // ---- the book's own "Chinese manner", p.126:
        // the message is written up and down the columns beginning on the RIGHT,
        // first column down, second up, and the grid is read off by rows.
        for (int w = 2; w <= 40; w++)
        {
            int h = (n + w - 1) / w;
            if (w * h - n >= w) continue;                 // more than one empty column
            for (int firstDown = 0; firstDown < 2; firstDown++)
                for (int fromRight = 0; fromRight < 2; fromRight++)
                {
                    var cell = new int[n];                // plaintext index -> grid cell
                    int k = 0; bool bad = false;
                    for (int j = 0; j < w && !bad; j++)
                    {
                        int c = fromRight == 1 ? w - 1 - j : j;
                        bool down = (j % 2 == 0) == (firstDown == 1);
                        for (int t = 0; t < h; t++)
                        {
                            int row = down ? t : h - 1 - t;
                            int idx = row * w + c;
                            if (idx >= n) { if (k < n) continue; else break; }
                            if (k >= n) { bad = true; break; }
                            cell[k++] = idx;
                        }
                    }
                    if (bad || k != n) continue;
                    var m = new int[n];
                    bool ok2 = true;
                    for (int i = 0; i < n; i++) { if (cell[i] >= n) { ok2 = false; break; } m[i] = cell[i]; }
                    if (!ok2) continue;
                    outp.Add(($"chinese manner w={w}{(firstDown == 1 ? "" : " up-first")}{(fromRight == 1 ? " from right" : " from left")}", m));
                }
        }

        // ---- rail fence
        for (int k = 2; k <= 30; k++)
        {
            var rail = new int[n];
            int rr = 0, dir = 1;
            var order = new List<int>[k];
            for (int i = 0; i < k; i++) order[i] = new List<int>();
            for (int i = 0; i < n; i++)
            {
                order[rr].Add(i);
                if (rr == 0) dir = 1; else if (rr == k - 1) dir = -1;
                rr += dir;
            }
            int p = 0;
            foreach (var lst in order) foreach (var i in lst) rail[i] = p++;
            outp.Add(($"rail fence k={k}", rail));
        }

        // ---- spiral and diagonal reads, for every exact rectangle
        for (int w = 2; w <= n / 2; w++)
        {
            if (n % w != 0) continue;
            int h = n / w;
            foreach (var (nm, cells) in Geometries(w, h))
            {
                var m = new int[n];
                for (int p = 0; p < n; p++) m[cells[p]] = p;   // plaintext cell -> ciphertext position
                outp.Add(($"{nm} {w}x{h}", m));
            }
        }
        // keep only well-formed permutations
        var ok = new List<(string, int[])>();
        foreach (var (nm, m) in outp)
        {
            if (m.Length != n) continue;
            var seen = new bool[n];
            bool good = true;
            foreach (var v in m) { if (v < 0 || v >= n || seen[v]) { good = false; break; } seen[v] = true; }
            if (good) ok.Add((nm, m));
        }
        return ok;
    }

    // returns, for each geometry, the sequence of grid cells in reading order
    static List<(string, int[])> Geometries(int w, int h)
    {
        var res = new List<(string, int[])>();
        int n = w * h;

        for (int variant = 0; variant < 4; variant++)
        {
            var seq = new List<int>();
            int top = 0, bot = h - 1, left = 0, right = w - 1;
            bool cw = variant < 2;
            bool fromTopLeft = variant % 2 == 0;
            top = 0; bot = h - 1; left = 0; right = w - 1;
            if (cw)
            {
                while (top <= bot && left <= right)
                {
                    for (int c = left; c <= right; c++) seq.Add(top * w + c);
                    top++;
                    for (int r2 = top; r2 <= bot; r2++) seq.Add(r2 * w + right);
                    right--;
                    if (top <= bot) { for (int c = right; c >= left; c--) seq.Add(bot * w + c); bot--; }
                    if (left <= right) { for (int r2 = bot; r2 >= top; r2--) seq.Add(r2 * w + left); left++; }
                }
            }
            else
            {
                while (top <= bot && left <= right)
                {
                    for (int r2 = top; r2 <= bot; r2++) seq.Add(r2 * w + left);
                    left++;
                    if (left <= right) { for (int c = left; c <= right; c++) seq.Add(bot * w + c); bot--; }
                    if (top <= bot) { for (int r2 = bot; r2 >= top; r2--) seq.Add(r2 * w + right); right--; }
                    if (left <= right) { for (int c = right; c >= left; c--) seq.Add(top * w + c); top++; }
                }
            }
            if (seq.Count != n) continue;
            if (!fromTopLeft) seq.Reverse();
            res.Add(($"spiral{variant}", seq.ToArray()));
        }

        // diagonals
        var d1 = new List<int>();
        for (int s = 0; s < w + h - 1; s++)
            for (int r2 = 0; r2 < h; r2++) { int c = s - r2; if (c >= 0 && c < w) d1.Add(r2 * w + c); }
        res.Add(("diagonal", d1.ToArray()));
        var d2 = new List<int>(d1); d2.Reverse();
        res.Add(("diagonal reversed", d2.ToArray()));
        return res;
    }

    public static void Sweep(string[] a)
    {
        int restarts = a.Length > 1 ? int.Parse(a[1]) : 30;
        string digits = a.Length > 2 ? a[2] : P.PRINTED.Replace(" ", "").Substring(0, 392);
        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length, n = ct.Length;
        var fo = Joint.FreqOrder(ct, S);

        var cands = Candidates(n);
        // add the reversed reading of every candidate
        var all = new List<(string name, int[] map)>(cands);
        foreach (var (nm, m) in cands)
        {
            var rv = new int[n];
            for (int i = 0; i < n; i++) rv[i] = m[n - 1 - i];
            all.Add((nm + " reversed", rv));
        }
        Console.WriteLine($"ROUTE SWEEP  {all.Count} unkeyed geometries x monoalphabetic solve");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");

        // always report the no-transposition baseline explicitly
        {
            var syms = (int[])ct.Clone();
            var (per0, _) = MonoSolve(syms, S, restarts, 5, fo);
            Console.WriteLine($"no transposition at all: {per0:F4} per letter");
        }

        var res = all.AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount)
            .Select(c =>
            {
                var syms = new int[n];
                for (int i = 0; i < n; i++) syms[i] = ct[c.map[i]];
                var (per, plain) = MonoSolve(syms, S, restarts, 31 + c.name.GetHashCode(), fo);
                return (c.name, per, plain);
            })
            .OrderByDescending(x => x.per).Take(15).ToList();

        Console.WriteLine($"{"per letter",11}  {"geometry",-30} plaintext");
        foreach (var (nm, per, plain) in res)
            Console.WriteLine($"{per,11:F4}  {nm,-30} {plain[..52]}");
    }
}
