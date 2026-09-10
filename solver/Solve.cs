using System.Text;

// Joint search over (column order, substitution) for a columnar transposition
// applied to a monoalphabetic substitution.
static class Joint
{
    public sealed class Res
    {
        public double Score;          // total 4-gram log10 prob
        public double Per;            // per letter
        public int[] Perm, Sigma;
        public string Plain;
        public int Width;
        public bool Inverse;
    }

    static readonly int[] EngRank = "ETAOINSHRDLCUMWFGYPBVKJXQZ".Select(c => c - 'A').ToArray();

    public static Res Run(int[] ct, int S, int w, bool inverse, long steps, int cycles, int seed, int[] freqOrder)
    {
        int n = ct.Length;
        var rnd = new Random(seed);
        var perm = new int[w]; var save = new int[w];
        var start = new int[w]; var len = new int[w];
        var mid = new int[n]; var L = new int[n];
        var sigma = new int[S]; var used = new bool[26];

        var best = new Res { Score = double.NegativeInfinity, Width = w, Inverse = inverse };

        void Rebuild()
        {
            if (inverse) P.Apply(ct, perm, start, len, mid);
            else P.Undo(ct, perm, start, len, mid);
            for (int i = 0; i < n; i++) L[i] = sigma[mid[i]];
        }
        void Letters() { for (int i = 0; i < n; i++) L[i] = sigma[mid[i]]; }
        void Snap(double cur)
        {
            if (cur <= best.Score) return;
            best.Score = cur; best.Per = cur / Math.Max(1, n - 3);
            best.Perm = (int[])perm.Clone(); best.Sigma = (int[])sigma.Clone();
            var sb = new StringBuilder(n);
            for (int i = 0; i < n; i++) sb.Append((char)('A' + L[i]));
            best.Plain = sb.ToString();
        }
        void Insert(int i, int j)
        {
            int v = perm[i];
            if (i < j) { for (int k = i; k < j; k++) perm[k] = perm[k + 1]; }
            else { for (int k = i; k > j; k--) perm[k] = perm[k - 1]; }
            perm[j] = v;
        }

        for (int cy = 0; cy < cycles; cy++)
        {
            // ---- seed: random column order, substitution matched by frequency rank
            for (int i = 0; i < w; i++) perm[i] = i;
            for (int i = w - 1; i > 0; i--) { int j = rnd.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
            Array.Clear(used);
            if (cy % 3 == 0 && freqOrder != null)
            {
                for (int i = 0; i < S; i++) { sigma[freqOrder[i]] = EngRank[i]; used[EngRank[i]] = true; }
            }
            else
            {
                var pool = Enumerable.Range(0, 26).OrderBy(_ => rnd.Next()).ToArray();
                for (int i = 0; i < S; i++) { sigma[i] = pool[i]; used[pool[i]] = true; }
            }

            Rebuild();
            double cur = P.Score(L);
            Snap(cur);

            double T0 = 9.0, T1 = 0.20;
            for (long s = 0; s < steps; s++)
            {
                double T = T0 * Math.Pow(T1 / T0, (double)s / steps);
                int k = rnd.Next(100);
                double ns;

                if (k < 45 && w > 1)                       // ---- column move
                {
                    Array.Copy(perm, save, w);
                    int m = rnd.Next(10);
                    if (m < 5) { int a = rnd.Next(w), b = rnd.Next(w); (perm[a], perm[b]) = (perm[b], perm[a]); }
                    else if (m < 8) { int a = rnd.Next(w), b = rnd.Next(w); Insert(a, b); }
                    else
                    {
                        int a = rnd.Next(w), b = rnd.Next(w); if (a > b) (a, b) = (b, a);
                        while (a < b) { (perm[a], perm[b]) = (perm[b], perm[a]); a++; b--; }
                    }
                    Rebuild(); ns = P.Score(L);
                    if (ns > cur || rnd.NextDouble() < Math.Exp((ns - cur) / T)) cur = ns;
                    else { Array.Copy(save, perm, w); Rebuild(); }
                }
                else if (k < 85)                           // ---- swap two letters
                {
                    int a = rnd.Next(S), b = rnd.Next(S);
                    if (a == b) continue;
                    (sigma[a], sigma[b]) = (sigma[b], sigma[a]);
                    Letters(); ns = P.Score(L);
                    if (ns > cur || rnd.NextDouble() < Math.Exp((ns - cur) / T)) cur = ns;
                    else { (sigma[a], sigma[b]) = (sigma[b], sigma[a]); Letters(); }
                }
                else                                       // ---- bring in an unused letter
                {
                    int a = rnd.Next(S), nl = rnd.Next(26);
                    if (used[nl]) continue;
                    int old = sigma[a];
                    sigma[a] = nl; used[old] = false; used[nl] = true;
                    Letters(); ns = P.Score(L);
                    if (ns > cur || rnd.NextDouble() < Math.Exp((ns - cur) / T)) cur = ns;
                    else { sigma[a] = old; used[nl] = false; used[old] = true; Letters(); }
                }
                if (cur > best.Score) Snap(cur);
            }

            // ---- deterministic polish
            Array.Copy(best.Perm, perm, w); Array.Copy(best.Sigma, sigma, S);
            Array.Clear(used); foreach (var v in sigma) used[v] = true;
            Rebuild(); cur = P.Score(L);
            bool imp = true;
            while (imp)
            {
                imp = false;
                for (int a = 0; a < S; a++)
                    for (int b = a + 1; b < S; b++)
                    {
                        (sigma[a], sigma[b]) = (sigma[b], sigma[a]); Letters();
                        double v = P.Score(L);
                        if (v > cur) { cur = v; imp = true; }
                        else { (sigma[a], sigma[b]) = (sigma[b], sigma[a]); Letters(); }
                    }
                for (int a = 0; a < S; a++)
                    for (int nl = 0; nl < 26; nl++)
                    {
                        if (used[nl]) continue;
                        int old = sigma[a]; sigma[a] = nl; used[old] = false; used[nl] = true;
                        Letters(); double v = P.Score(L);
                        if (v > cur) { cur = v; imp = true; }
                        else { sigma[a] = old; used[nl] = false; used[old] = true; Letters(); }
                    }
                for (int a = 0; a < w; a++)
                    for (int b = 0; b < w; b++)
                    {
                        if (a == b) continue;
                        Array.Copy(perm, save, w); Insert(a, b);
                        Rebuild(); double v = P.Score(L);
                        if (v > cur) { cur = v; imp = true; }
                        else { Array.Copy(save, perm, w); Rebuild(); }
                    }
                Snap(cur);
            }
        }
        return best;
    }

    // frequency order of symbols, most common first
    public static int[] FreqOrder(int[] ct, int S)
    {
        var c = new int[S];
        foreach (var x in ct) c[x]++;
        return Enumerable.Range(0, S).OrderByDescending(i => c[i]).ToArray();
    }
}
