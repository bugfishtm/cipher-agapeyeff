using System.Text;

static class P
{
    // ---------------------------------------------------------------- corpus / language model
    public static float[] LP;                 // 26^4 interpolated conditional log10 P(d | a b c)
    public static string Corpus;

    public static void BuildModel(string path)
    {
        Corpus = File.ReadAllText(path).Trim();
        int N = Corpus.Length;
        var t = new int[N];
        for (int i = 0; i < N; i++) t[i] = Corpus[i] - 'A';

        var c1 = new int[26];
        var c2 = new int[676];
        var c3 = new int[17576];
        var c4 = new int[456976];
        for (int i = 0; i < N; i++) c1[t[i]]++;
        for (int i = 0; i + 1 < N; i++) c2[t[i] * 26 + t[i + 1]]++;
        for (int i = 0; i + 2 < N; i++) c3[t[i] * 676 + t[i + 1] * 26 + t[i + 2]]++;
        for (int i = 0; i + 3 < N; i++) c4[t[i] * 17576 + t[i + 1] * 676 + t[i + 2] * 26 + t[i + 3]]++;

        double nu = 4, mu = 6, lam = 8;
        var p1 = new double[26];
        for (int d = 0; d < 26; d++) p1[d] = (c1[d] + 1.0) / (N + 26.0);

        var p2 = new double[676];
        for (int c = 0; c < 26; c++)
            for (int d = 0; d < 26; d++)
                p2[c * 26 + d] = (c2[c * 26 + d] + nu * p1[d]) / (c1[c] + nu);

        var p3 = new double[17576];
        for (int b = 0; b < 26; b++)
            for (int c = 0; c < 26; c++)
            {
                double den = c2[b * 26 + c] + mu;
                for (int d = 0; d < 26; d++)
                    p3[b * 676 + c * 26 + d] = (c3[b * 676 + c * 26 + d] + mu * p2[c * 26 + d]) / den;
            }

        LP = new float[456976];
        for (int a = 0; a < 26; a++)
            for (int b = 0; b < 26; b++)
                for (int c = 0; c < 26; c++)
                {
                    double den = c3[a * 676 + b * 26 + c] + lam;
                    int baseIdx = a * 17576 + b * 676 + c * 26;
                    for (int d = 0; d < 26; d++)
                        LP[baseIdx + d] = (float)Math.Log10((c4[baseIdx + d] + lam * p3[b * 676 + c * 26 + d]) / den);
                }
    }

    [ThreadStatic] public static int HeadLimit;   // 0 = score the whole text
    public static double Score(int[] L)
    {
        int n = HeadLimit > 0 ? Math.Min(HeadLimit, L.Length) : L.Length;
        double s = 0;
        for (int i = 3; i < n; i++)
            s += LP[L[i - 3] * 17576 + L[i - 2] * 676 + L[i - 1] * 26 + L[i]];
        return s / (HeadLimit > 0 ? Math.Max(1, n - 3) / (double)Math.Max(1, L.Length - 3) : 1.0);
    }
    public static double ScorePer(int[] L) => Score(L) / Math.Max(1, L.Length - 3);
    public static double ScoreText(string s)
    {
        var L = new int[s.Length];
        for (int i = 0; i < s.Length; i++) L[i] = s[i] - 'A';
        return ScorePer(L);
    }

    // ---------------------------------------------------------------- the cipher
    public const string PRINTED =
        "75628285916291648164917485846474748284838163818174" +
        "74826264758382849175746583757575936365658163817585" +
        "75756462829285746382757483816581848564856485856382" +
        "72628362818172816463758281648363828581636363047481" +
        "91918463858465648565629462628591859174917275646575" +
        "71658362647481828462826491819365626484849183857491" +
        "81657274838385828364627262656283759272638282727283" +
        "82858475828183728462828375816475748581629 2000";

    public static int[] CipherUnits(string digits, out string[] names)
    {
        var d = digits.Replace(" ", "");
        var units = new List<string>();
        for (int i = 0; i + 1 < d.Length; i += 2) units.Add(d.Substring(i, 2));
        var uniq = units.Distinct().OrderBy(x => x).ToArray();
        names = uniq;
        var idx = uniq.Select((u, i) => (u, i)).ToDictionary(p => p.u, p => p.i);
        return units.Select(u => idx[u]).ToArray();
    }

    // ---------------------------------------------------------------- columnar transposition
    // ciphertext = columns of a w-wide rectangle, read off in the order given by perm.
    // Undo: plain[i] = ct[start[i % w] + i / w]
    public static void ColStarts(int n, int[] perm, int[] start, int[] len)
    {
        int w = perm.Length, q = n / w, r = n % w;
        for (int c = 0; c < w; c++) len[c] = c < r ? q + 1 : q;
        int p = 0;
        for (int k = 0; k < w; k++) { start[perm[k]] = p; p += len[perm[k]]; }
    }
    public static void Undo(int[] ct, int[] perm, int[] start, int[] len, int[] outp)
    {
        int w = perm.Length, n = ct.Length;
        ColStarts(n, perm, start, len);
        for (int i = 0; i < n; i++) outp[i] = ct[start[i % w] + i / w];
    }
    public static void Apply(int[] pt, int[] perm, int[] start, int[] len, int[] outc)
    {
        int w = perm.Length, n = pt.Length;
        ColStarts(n, perm, start, len);
        for (int i = 0; i < n; i++) outc[start[i % w] + i / w] = pt[i];
    }

    // ---------------------------------------------------------------- joint annealing
    public sealed class Result
    {
        public double Score;
        public int[] Perm;
        public int[] Sigma;
        public string Plain;
        public int Width;
        public bool Inverse;
    }

    public static Result Solve(int[] ct, int S, int w, bool inverse, int restarts, int steps, int seed)
    {
        int n = ct.Length;
        var best = new Result { Score = double.NegativeInfinity, Width = w, Inverse = inverse };
        var rnd = new Random(seed);

        var perm = new int[w];
        var start = new int[w];
        var len = new int[w];
        var mid = new int[n];
        var L = new int[n];
        var sigma = new int[S];
        var used = new bool[26];

        void Rebuild()
        {
            if (inverse) Apply(ct, perm, start, len, mid);
            else Undo(ct, perm, start, len, mid);
            for (int i = 0; i < n; i++) L[i] = sigma[mid[i]];
        }
        void RebuildLetters() { for (int i = 0; i < n; i++) L[i] = sigma[mid[i]]; }

        for (int r = 0; r < restarts; r++)
        {
            for (int i = 0; i < w; i++) perm[i] = i;
            for (int i = w - 1; i > 0; i--) { int j = rnd.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
            var pool = Enumerable.Range(0, 26).OrderBy(_ => rnd.Next()).ToArray();
            Array.Clear(used);
            for (int i = 0; i < S; i++) { sigma[i] = pool[i]; used[pool[i]] = true; }

            Rebuild();
            double cur = Score(L);
            double T0 = 12.0, T1 = 0.25;
            for (int s = 0; s < steps; s++)
            {
                double T = T0 * Math.Pow(T1 / T0, (double)s / steps);
                int kind = rnd.Next(100);
                if (kind < 42 && w > 1)
                {
                    int a = rnd.Next(w), b = rnd.Next(w);
                    if (a == b) continue;
                    (perm[a], perm[b]) = (perm[b], perm[a]);
                    Rebuild();
                    double ns = Score(L);
                    if (ns > cur || rnd.NextDouble() < Math.Exp((ns - cur) / T)) cur = ns;
                    else { (perm[a], perm[b]) = (perm[b], perm[a]); Rebuild(); }
                }
                else if (kind < 85)
                {
                    int a = rnd.Next(S), b = rnd.Next(S);
                    if (a == b) continue;
                    (sigma[a], sigma[b]) = (sigma[b], sigma[a]);
                    RebuildLetters();
                    double ns = Score(L);
                    if (ns > cur || rnd.NextDouble() < Math.Exp((ns - cur) / T)) cur = ns;
                    else { (sigma[a], sigma[b]) = (sigma[b], sigma[a]); RebuildLetters(); }
                }
                else
                {
                    int a = rnd.Next(S), nl = rnd.Next(26);
                    if (used[nl]) continue;
                    int old = sigma[a];
                    sigma[a] = nl; used[old] = false; used[nl] = true;
                    RebuildLetters();
                    double ns = Score(L);
                    if (ns > cur || rnd.NextDouble() < Math.Exp((ns - cur) / T)) cur = ns;
                    else { sigma[a] = old; used[nl] = false; used[old] = true; RebuildLetters(); }
                }

                if (cur > best.Score)
                {
                    best.Score = cur;
                    best.Perm = (int[])perm.Clone();
                    best.Sigma = (int[])sigma.Clone();
                    var sb = new StringBuilder(n);
                    for (int i = 0; i < n; i++) sb.Append((char)('A' + L[i]));
                    best.Plain = sb.ToString();
                }
            }

            // greedy polish from the annealed state
            bool imp = true;
            while (imp)
            {
                imp = false;
                for (int a = 0; a < S; a++)
                    for (int b = a + 1; b < S; b++)
                    {
                        (sigma[a], sigma[b]) = (sigma[b], sigma[a]);
                        RebuildLetters();
                        double ns = Score(L);
                        if (ns > cur) { cur = ns; imp = true; }
                        else { (sigma[a], sigma[b]) = (sigma[b], sigma[a]); RebuildLetters(); }
                    }
                for (int a = 0; a < w; a++)
                    for (int b = a + 1; b < w; b++)
                    {
                        (perm[a], perm[b]) = (perm[b], perm[a]);
                        Rebuild();
                        double ns = Score(L);
                        if (ns > cur) { cur = ns; imp = true; }
                        else { (perm[a], perm[b]) = (perm[b], perm[a]); Rebuild(); }
                    }
                if (cur > best.Score)
                {
                    best.Score = cur;
                    best.Perm = (int[])perm.Clone();
                    best.Sigma = (int[])sigma.Clone();
                    var sb = new StringBuilder(n);
                    for (int i = 0; i < n; i++) sb.Append((char)('A' + L[i]));
                    best.Plain = sb.ToString();
                }
            }
        }
        return best;
    }
}

class Program
{
    static void Main(string[] argv)
    {
        string scr = AppContext.BaseDirectory;
        string corpus = Path.Combine(Directory.GetCurrentDirectory(), "corpus.txt");
        if (!File.Exists(corpus)) corpus = Path.Combine(scr, "corpus.txt");
        P.BuildModel(corpus);

        string mode = argv.Length > 0 ? argv[0] : "calibrate";

        Dp.BuildBigram(P.Corpus);
        if (mode == "calibrate") { Calibrate(); return; }
        if (mode == "dpcontrol") { Modes.DpControl(argv); return; }
        if (mode == "dpsweep") { Modes.DpSweep(argv); return; }
        if (mode == "dpattack") { Modes.DpAttack(argv); return; }
        if (mode == "routes") { Routes.Sweep(argv); return; }
        if (mode == "digitt") { DigitT.Attack(argv); return; }
        if (mode == "dbl") { Double.Attack(argv); return; }
        if (mode == "deep") { Deep.Run(argv); return; }
        if (mode == "words") { Words.Attack(argv); return; }
        if (mode == "irr") { Modes.Irregular(argv); return; }
        if (mode == "nulls") { Nulls.Attack(argv); return; }
        if (mode == "rev") { Reverse.Attack(argv); return; }
        if (mode == "head") { Head.Attack(argv); return; }
        if (mode == "dbl4") { Double4.Attack(argv); return; }
        if (mode == "halves") { Halves.Attack(argv); return; }
        if (mode == "crib") { Crib.Attack(argv); return; }
        if (mode == "control") { Control(argv); return; }
        if (mode == "attack") { Attack(argv); return; }
        Console.WriteLine("modes: calibrate | control <width> <restarts> <steps> | attack <maxwidth> <restarts> <steps>");
    }

    static void Calibrate()
    {
        var rnd = new Random(7);
        var scores = new List<double>();
        for (int i = 0; i < 400; i++)
        {
            int p = rnd.Next(P.Corpus.Length - 200);
            scores.Add(P.ScoreText(P.Corpus.Substring(p, 196)));
        }
        scores.Sort();
        Console.WriteLine($"real English, 196 letters : mean {scores.Average():F4}  p05 {scores[20]:F4}  p95 {scores[380]:F4}");

        var sh = new List<double>();
        for (int i = 0; i < 200; i++)
        {
            int p = rnd.Next(P.Corpus.Length - 200);
            var a = P.Corpus.Substring(p, 196).ToCharArray();
            for (int k = a.Length - 1; k > 0; k--) { int j = rnd.Next(k + 1); (a[k], a[j]) = (a[j], a[k]); }
            sh.Add(P.ScoreText(new string(a)));
        }
        sh.Sort();
        Console.WriteLine($"the same text, shuffled   : mean {sh.Average():F4}  p95 {sh[190]:F4}");

        var rr = new List<double>();
        for (int i = 0; i < 200; i++)
        {
            var a = new char[196];
            for (int k = 0; k < 196; k++) a[k] = (char)('A' + rnd.Next(26));
            rr.Add(P.ScoreText(new string(a)));
        }
        Console.WriteLine($"uniform random letters    : mean {rr.Average():F4}");
    }

    static void Control(string[] argv)
    {
        int w = argv.Length > 1 ? int.Parse(argv[1]) : 11;
        int cycles = argv.Length > 2 ? int.Parse(argv[2]) : 40;
        long steps = argv.Length > 3 ? long.Parse(argv[3]) : 300000;
        int seedOff = argv.Length > 4 ? int.Parse(argv[4]) : 0;
        var rnd = new Random(20250823 + seedOff);

        int off = 60000;
        string pt = P.Corpus.Substring(off, 196);
        var sq = "MANCHESTRBDFGIKLOPQUVWXYZ";
        var cell = new Dictionary<char, int>();
        for (int i = 0; i < 25; i++) cell[sq[i]] = i;
        var ptl = pt.Replace('J', 'I');
        var syms = ptl.Select(c => cell.ContainsKey(c) ? cell[c] : cell['X']).ToArray();

        var perm = Enumerable.Range(0, w).OrderBy(_ => rnd.Next()).ToArray();
        var ctArr = new int[syms.Length];
        var st = new int[w]; var ln = new int[w];
        P.Apply(syms, perm, st, ln, ctArr);
        var uniq = ctArr.Distinct().OrderBy(x => x).ToArray();
        var map = uniq.Select((u, i) => (u, i)).ToDictionary(q => q.u, q => q.i);
        var ct = ctArr.Select(x => map[x]).ToArray();
        int S = uniq.Length;

        Console.WriteLine($"CONTROL  width {w}  true perm [{string.Join(",", perm)}]  {S} distinct symbols");
        Console.WriteLine($"  plaintext  : {ptl[..70]}...");
        Console.WriteLine($"  true score : {P.ScoreText(ptl):F4} per letter");

        var fo = Joint.FreqOrder(ct, S);
        int T = Environment.ProcessorCount;
        var res = Enumerable.Range(0, T).AsParallel().WithDegreeOfParallelism(T)
            .Select(k => Joint.Run(ct, S, w, false, steps, cycles, 4242 + 91 * k + seedOff, fo))
            .OrderByDescending(r => r.Score).First();

        int match = 0;
        for (int i = 0; i < 196; i++) if (res.Plain[i] == ptl[i]) match++;
        Console.WriteLine($"  RECOVERED  : {res.Per:F4} per letter   perm [{string.Join(",", res.Perm)}]");
        Console.WriteLine($"  {res.Plain[..98]}");
        Console.WriteLine($"  {res.Plain[98..]}");
        Console.WriteLine($"  letters correct: {match}/196 ({100.0 * match / 196:F1}%)   {(match > 170 ? "SOLVED" : "FAILED")}");
    }

    static void Attack(string[] argv)
    {
        int minw = argv.Length > 1 ? int.Parse(argv[1]) : 2;
        int maxw = argv.Length > 2 ? int.Parse(argv[2]) : 20;
        int cycles = argv.Length > 3 ? int.Parse(argv[3]) : 30;
        long steps = argv.Length > 4 ? long.Parse(argv[4]) : 300000;
        string digits = argv.Length > 5 ? argv[5] : P.PRINTED.Replace(" ", "").Substring(0, 392);

        var ct = P.CipherUnits(digits, out var names);
        int S = names.Length;
        var fo = Joint.FreqOrder(ct, S);
        int T = Environment.ProcessorCount;
        Console.WriteLine($"ATTACK  {ct.Length} units, {S} distinct: {string.Join(" ", names)}");
        Console.WriteLine($"widths {minw}..{maxw}, both directions, {cycles} cycles x {steps:N0} steps, {T} threads");
        Console.WriteLine($"reference: real English -0.74, shuffled English -1.89, random -2.36");
        Console.WriteLine();

        var jobs = new List<(int w, bool inv, int rep)>();
        for (int w = minw; w <= maxw; w++)
            for (int rep = 0; rep < 2; rep++)
            { jobs.Add((w, false, rep)); if (w > 1) jobs.Add((w, true, rep)); }

        var all = jobs.AsParallel().WithDegreeOfParallelism(T)
            .Select(j => Joint.Run(ct, S, j.w, j.inv, steps, cycles, 7717 * j.w + (j.inv ? 5 : 0) + 31 * j.rep, fo))
            .ToList();

        var byWidth = all.GroupBy(r => (r.Width, r.Inverse))
                         .Select(g => g.OrderByDescending(x => x.Score).First())
                         .OrderBy(r => r.Width).ThenBy(r => r.Inverse).ToList();

        Console.WriteLine($"{"w",3} {"dir",4} {"per letter",11}   first 58 letters");
        foreach (var r in byWidth)
            Console.WriteLine($"{r.Width,3} {(r.Inverse ? "inv" : "fwd"),4} {r.Per,11:F4}   {r.Plain[..58]}");

        Console.WriteLine();
        Console.WriteLine("BEST FIVE");
        foreach (var r in all.OrderByDescending(x => x.Score).Take(5))
        {
            Console.WriteLine($"  w={r.Width} {(r.Inverse ? "inv" : "fwd")}  {r.Per:F4}  perm [{string.Join(",", r.Perm)}]");
            Console.WriteLine($"    {r.Plain}");
        }
    }
}
