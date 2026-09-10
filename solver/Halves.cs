using System.Text;

// Priority 3 and 4: the midpoint split, and the possibility that the tail is not
// three nulls but something else.
//
// The single anomalous unit (04) is unit 98 of 196 - the exact midpoint, and the
// last cell of row 7 in the 14x14 grid. If that is a divider rather than a letter,
// the two halves may have been enciphered separately, or with different keys, or
// the second half written backwards.
//
// Separately: seven printed lines of ten groups and a last line of nine. Eighty
// groups would have been the natural count. A group lost from the end is invisible
// to the parity test, so the true unit count may not be 196.
static class Halves
{
    static void Sweep(string label, int[] units, int S, int wlo, int whi, int seeds)
    {
        if (units.Length < 40) { Console.WriteLine($"  {label,-34} too short"); return; }
        var fo = Joint.FreqOrder(units, S);
        var res = Enumerable.Range(wlo, whi - wlo + 1).AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .Select(w => Dp.Attack(units, S, w, seeds, 3131 + w, fo))
            .Where(x => x != null).OrderByDescending(x => x.Per).ToList();
        if (res.Count == 0) { Console.WriteLine($"  {label,-34} no arrangement"); return; }
        var b = res[0];
        Console.WriteLine($"  {label,-34} w={b.W,2}  {b.Per,8:F4}   {b.Plain[..Math.Min(44, b.Plain.Length)]}");
    }

    public static void Attack(string[] a)
    {
        int wlo = a.Length > 1 ? int.Parse(a[1]) : 2;
        int whi = a.Length > 2 ? int.Parse(a[2]) : 13;
        int seeds = a.Length > 3 ? int.Parse(a[3]) : 120;

        string full = P.PRINTED.Replace(" ", "");
        var ct = P.CipherUnits(full.Substring(0, 392), out var names);
        int S = names.Length, n = ct.Length;

        Console.WriteLine("MIDPOINT SPLIT AND TAIL LENGTH");
        Console.WriteLine("reference: real English -0.74 (p05 -0.85), shuffled English -1.89");
        Console.WriteLine();

        Console.WriteLine("A. the message cut at the midpoint, each half swept on its own");
        int mid = 98;                                   // 04 is unit 98 (1-based)
        Sweep("first half, units 1-97", ct.Take(97).ToArray(), S, wlo, Math.Min(whi, 10), seeds);
        Sweep("second half, units 99-196", ct.Skip(98).ToArray(), S, wlo, Math.Min(whi, 10), seeds);
        Sweep("first half incl. the 04", ct.Take(98).ToArray(), S, wlo, Math.Min(whi, 10), seeds);
        Console.WriteLine();

        Console.WriteLine("B. halves recombined");
        var swapped = ct.Skip(98).Concat(ct.Take(98)).ToArray();
        Sweep("halves swapped", swapped, S, wlo, whi, seeds);
        var secondRev = ct.Take(98).Concat(ct.Skip(98).Reverse()).ToArray();
        Sweep("second half reversed", secondRev, S, wlo, whi, seeds);
        var firstRev = ct.Take(98).Reverse().Concat(ct.Skip(98)).ToArray();
        Sweep("first half reversed", firstRev, S, wlo, whi, seeds);
        var noMid = ct.Take(97).Concat(ct.Skip(98)).ToArray();
        Sweep("the 04 removed entirely (195)", noMid, S, wlo, whi, seeds);
        Console.WriteLine();

        Console.WriteLine("C. the tail is not what we assumed - other unit counts");
        for (int drop = 0; drop <= 8; drop += 2)
        {
            int keep = 392 - drop;
            if (keep % 2 != 0) continue;
            var t = P.CipherUnits(full.Substring(0, Math.Min(keep, full.Length)), out var nm2);
            Sweep($"{t.Length} units (drop {drop} digits)", t, nm2.Length, wlo, whi, seeds);
        }
        // and the reading that keeps all 395 digits: 197 units, last one "00"
        var t397 = P.CipherUnits(full, out var nm3);
        Sweep($"{t397.Length} units (all 395 digits)", t397, nm3.Length, wlo, whi, seeds);
        Console.WriteLine();

        Console.WriteLine("D. wide keys where 392 divides evenly");
        var fo = Joint.FreqOrder(ct, S);
        foreach (int w in new[] { 28, 49, 56 })
        {
            var hit = Dp.Attack(ct, S, w, 400, 77 + w, fo);
            Console.WriteLine(hit == null
                ? $"  w={w,-3} no arrangement"
                : $"  w={w,-3} {hit.Per,8:F4}   {hit.Plain[..Math.Min(44, hit.Plain.Length)]}");
        }
    }
}
