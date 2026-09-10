using System.Text;

// Exact column ordering for a columnar transposition, given a substitution guess.
// The order of the columns is solved optimally by Held-Karp over subsets, using
// bigram statistics between vertically-aligned symbols of adjacent columns.
// The substitution is then refined against the 4-gram model and the two alternate
// until neither improves.
static class Dp
{
    public static double[] LB;         // 26*26 : log10 P(second | first)

    public static void BuildBigram(string corpus)
    {
        var c1 = new int[26];
        var c2 = new int[676];
        for (int i = 0; i < corpus.Length; i++) c1[corpus[i] - 'A']++;
        for (int i = 0; i + 1 < corpus.Length; i++) c2[(corpus[i] - 'A') * 26 + (corpus[i + 1] - 'A')]++;
        int N = corpus.Length;
        LB = new double[676];
        for (int a = 0; a < 26; a++)
            for (int b = 0; b < 26; b++)
            {
                double pb = (c1[b] + 1.0) / (N + 26.0);
                LB[a * 26 + b] = Math.Log10((c2[a * 26 + b] + 3.0 * pb) / (c1[a] + 3.0));
            }
    }

    public sealed class Hit
    {
        public double Per;
        public int W;
        public int[] BlockOrder;     // block index sitting at each column position
        public int[] LongSet;        // which ciphertext blocks were taken as long
        public int[] Sigma;
        public string Plain;
    }

    // split ciphertext into w blocks; longMask marks which blocks (in ciphertext order) are long
    static int[][] Split(int[] ct, int w, int q, int r, int longMask)
    {
        var blocks = new int[w][];
        int p = 0;
        for (int k = 0; k < w; k++)
        {
            int L = ((longMask >> k) & 1) == 1 ? q + 1 : q;
            blocks[k] = new int[L];
            Array.Copy(ct, p, blocks[k], 0, L);
            p += L;
        }
        return blocks;
    }

    // assemble the plaintext given which block sits at each column position
    static int[] Assemble(int[][] blocks, int[] order, int n)
    {
        int w = order.Length;
        var outp = new int[n];
        int idx = 0;
        int rows = 0;
        foreach (var b in blocks) rows = Math.Max(rows, b.Length);
        for (int row = 0; row < rows; row++)
            for (int p = 0; p < w; p++)
            {
                var b = blocks[order[p]];
                if (row < b.Length) outp[idx++] = b[row];
            }
        return outp;
    }

    static int[] BestOrder(int[][] blocks, int[] posLen, int[] sigma, double[] dp, int[] par)
    {
        int w = blocks.Length;
        var adj = new double[w * w];
        for (int a = 0; a < w; a++)
            for (int b = 0; b < w; b++)
            {
                if (a == b) continue;
                int m = Math.Min(blocks[a].Length, blocks[b].Length);
                if (P.HeadLimit > 0) m = Math.Min(m, (P.HeadLimit + w - 1) / w);
                double s = 0;
                for (int i = 0; i < m; i++) s += LB[sigma[blocks[a][i]] * 26 + sigma[blocks[b][i]]];
                adj[a * w + b] = s;
            }

        int full = 1 << w;
        Array.Fill(dp, double.NegativeInfinity);
        for (int b = 0; b < w; b++)
            if (posLen == null || blocks[b].Length == posLen[0]) { dp[(1 << b) * w + b] = 0; par[(1 << b) * w + b] = -1; }

        for (int mask = 1; mask < full; mask++)
        {
            int pc = System.Numerics.BitOperations.PopCount((uint)mask);
            if (pc >= w) continue;
            int need = posLen == null ? -1 : posLen[pc];
            for (int last = 0; last < w; last++)
            {
                if ((mask >> last & 1) == 0) continue;
                double cur = dp[mask * w + last];
                if (double.IsNegativeInfinity(cur)) continue;
                for (int nxt = 0; nxt < w; nxt++)
                {
                    if ((mask >> nxt & 1) == 1) continue;
                    if (need >= 0 && blocks[nxt].Length != need) continue;
                    int nm = mask | (1 << nxt);
                    double v = cur + adj[last * w + nxt];
                    if (v > dp[nm * w + nxt]) { dp[nm * w + nxt] = v; par[nm * w + nxt] = last; }
                }
            }
        }

        int bestLast = -1; double bestV = double.NegativeInfinity;
        for (int b = 0; b < w; b++)
            if (dp[(full - 1) * w + b] > bestV) { bestV = dp[(full - 1) * w + b]; bestLast = b; }
        if (bestLast < 0) return null;

        var order = new int[w];
        int mk = full - 1, cu = bestLast;
        for (int i = w - 1; i >= 0; i--)
        {
            order[i] = cu;
            int pv = par[mk * w + cu];
            mk ^= 1 << cu;
            cu = pv;
        }
        return order;
    }

    // beam search over column order, for widths where 2^w is out of reach
    static int[] BestOrderBeam(int[][] blocks, int[] posLen, int[] sigma, int beam)
    {
        int w = blocks.Length;
        var adj = new double[w * w];
        for (int a = 0; a < w; a++)
            for (int b = 0; b < w; b++)
            {
                if (a == b) continue;
                int m = Math.Min(blocks[a].Length, blocks[b].Length);
                double s = 0;
                for (int i = 0; i < m; i++) s += LB[sigma[blocks[a][i]] * 26 + sigma[blocks[b][i]]];
                adj[a * w + b] = s;
            }

        var cur = new List<(ulong used, int last, double sc, int[] ord)>();
        for (int b = 0; b < w; b++)
            if (posLen == null || blocks[b].Length == posLen[0])
                cur.Add((1UL << b, b, 0.0, new[] { b }));
        if (cur.Count == 0) return null;

        for (int p = 1; p < w; p++)
        {
            int need = posLen == null ? -1 : posLen[p];
            var nxt = new List<(ulong used, int last, double sc, int[] ord)>(cur.Count * 4);
            foreach (var st in cur)
                for (int b = 0; b < w; b++)
                {
                    if ((st.used >> b & 1) == 1) continue;
                    if (need >= 0 && blocks[b].Length != need) continue;
                    var ord = new int[p + 1];
                    Array.Copy(st.ord, ord, p);
                    ord[p] = b;
                    nxt.Add((st.used | (1UL << b), b, st.sc + adj[st.last * w + b], ord));
                }
            if (nxt.Count == 0) return null;
            nxt.Sort((x, y) => y.sc.CompareTo(x.sc));
            if (nxt.Count > beam) nxt.RemoveRange(beam, nxt.Count - beam);
            cur = nxt;
        }
        return cur[0].ord;
    }

    // hill-climb the substitution against the 4-gram model
    static double PolishSigma(int[] syms, int S, int[] sigma, int[] L, bool[] used)
    {
        int n = syms.Length;
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
                    else { (sigma[a], sigma[b]) = (sigma[b], sigma[a]); }
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
        for (int i = 0; i < n; i++) L[i] = sigma[syms[i]];
        return cur;
    }

    static readonly int[] EngRank = "ETAOINSHRDLCUMWFGYPBVKJXQZ".Select(c => c - 'A').ToArray();

    public static Hit Attack(int[] ct, int S, int w, int seeds, int seed, int[] freqOrder, bool irregular = false)
    {
        int n = ct.Length, q = n / w, r = n % w;
        var posLen = new int[w];
        for (int p = 0; p < w; p++) posLen[p] = p < r ? q + 1 : q;
        // an encipherer working by hand often puts the short cells in the wrong columns.
        // in irregular mode any block may sit at any position, which covers that slip.
        int[] posLenFree = null;
        if (irregular) posLenFree = null;

        bool exact = w <= 15;
        if (!exact && r != 0)
            return null;    // too wide to enumerate the long-column patterns

        var patterns = new List<int>();
        if (exact)
        {
            for (int mask = 0; mask < (1 << w); mask++)
                if (System.Numerics.BitOperations.PopCount((uint)mask) == r) patterns.Add(mask);
        }
        else patterns.Add(0);

        var dpArr = exact ? new double[(1 << w) * w] : null;
        var parArr = exact ? new int[(1 << w) * w] : null;
        var sigma = new int[S];
        var used = new bool[26];
        var L = new int[n];
        var rnd = new Random(seed);

        Hit best = null;

        foreach (int pat in patterns)
        {
            var blocks = Split(ct, w, q, r, pat);
            for (int t = 0; t < seeds; t++)
            {
                Array.Clear(used);
                if (t == 0)
                {
                    for (int i = 0; i < S; i++) { sigma[freqOrder[i]] = EngRank[i]; used[EngRank[i]] = true; }
                }
                else
                {
                    var pool = Enumerable.Range(0, 26).OrderBy(_ => rnd.Next()).ToArray();
                    for (int i = 0; i < S; i++) { sigma[i] = pool[i]; used[pool[i]] = true; }
                }

                int[] order = null;
                double sc = double.NegativeInfinity;
                for (int it = 0; it < 8; it++)
                {
                    var lens = irregular ? posLenFree : posLen;
                    var ord = exact ? BestOrder(blocks, lens, sigma, dpArr, parArr)
                                    : BestOrderBeam(blocks, lens, sigma, 3000);
                    if (ord == null) break;
                    var syms = Assemble(blocks, ord, n);
                    double v = PolishSigma(syms, S, sigma, L, used);
                    if (order != null && ord.SequenceEqual(order) && v <= sc) { sc = v; break; }
                    order = ord; sc = v;
                }
                if (order == null) continue;
                double per = sc / (n - 3);
                if (best == null || per > best.Per)
                {
                    var syms = Assemble(blocks, order, n);
                    for (int i = 0; i < n; i++) L[i] = sigma[syms[i]];
                    var sb = new StringBuilder(n);
                    for (int i = 0; i < n; i++) sb.Append((char)('A' + L[i]));
                    best = new Hit
                    {
                        Per = per,
                        W = w,
                        BlockOrder = (int[])order.Clone(),
                        LongSet = Enumerable.Range(0, w).Where(k => (pat >> k & 1) == 1).ToArray(),
                        Sigma = (int[])sigma.Clone(),
                        Plain = sb.ToString()
                    };
                }
            }
        }
        return best;
    }
}
