# D'Agapeyeff cipher — attack toolkit

A native-speed cryptanalysis harness for the challenge cipher on page 159 of
Alexander d'Agapeyeff, *Codes and Ciphers* (Oxford University Press, 1939).

Everything here is reproducible. Every negative result quoted in
the investigation site (`docs/`) came out of this code, and every attack was first
proved against a control — English put through a keyword square and a real
columnar transposition, then handed to the same solver with nothing else told
to it.

## Build and run

```bash
cd solver
dotnet build -c Release
cp corpus.txt words.txt bin/Release/net10.0/
./bin/Release/net10.0/solver.exe calibrate
```

Needs the .NET 10 SDK. Uses every core you have.

## The language model

An interpolated four-gram model (four-gram backing off to trigram, bigram,
unigram, add-λ at each level) built from `corpus.txt` — 147,626 letters of the
book's own text, which is the right register for a 1939 British plaintext.

Scores are mean log₁₀ probability per letter. The scale that matters:

| | score |
|---|---|
| real English, 196 letters | **−0.74** (5th pct −0.85) |
| the same text shuffled | **−1.89** |
| uniform random letters | **−2.36** |

`calibrate` reprints these from your build.

## Modes

| command | what it does |
|---|---|
| `calibrate` | prints the reference scores above |
| `control <w> <cycles> <steps>` | joint annealing on a known cipher of width `w` — demonstrates that annealing *fails* at this length |
| `dpcontrol <wlo> <whi> <seeds>` | the real attack, run against a known cipher at each width. Should print 196/196 |
| `dpsweep <trueW> <wlo> <whi> <seeds>` | blind width sweep on a known cipher — shows the true width standing clear of the rest |
| `dpattack <wlo> <whi> <seeds> [digits]` | **the main attack.** Keyed columnar transposition of whole units, column order solved exactly |
| `digitt <wlo> <whi> <deep> [digits]` | transposition of single digits. Enumerates every key that leaves the 6789/12345 alternation intact |
| `routes <restarts> [digits]` | 614 unkeyed geometries — columnar, boustrophedon, the book's "Chinese manner" (p.126), rail fence, spirals, diagonals, each also reversed |
| `dbl same <wlo> <whi> [digits]` | double transposition, one key applied twice |
| `dbl pair <wlo> <whi> [digits]` | double transposition, two independent keys |
| `words <file> <maxw> [digits]` | real words and dates as transposition keys, numbered by alphabetical rank as on p.117 |
| `irr <wlo> <whi> <seeds> [digits]` | irregular columnar — the short cells allowed in the *wrong* columns, the commonest hand slip |
| `deep control\|attack <w> <seedsPerThread>` | one width, many substitution seeds spread over every core. Needed past width 12 |
| `nulls pos\|sym\|both <restarts>` | dummy letters — p.111's own suggestion. Positional strides, null cells, and both |
| `rev <wlo> <whi> <restarts>` | the transposition run backwards, per the 2017 Cipher Mysteries finding. The natural-order column needs no key search and is a clean width detector |
| `head <wlo> <whi> <seeds>` | **priority 1.** Scores only the first N letters, sweeping N. A genuine partial solve shows as a plateau near the English reference that falls off a cliff |
| `halves <wlo> <whi> <seeds>` | the midpoint split, other unit counts, and the widths that divide 392 evenly |
| `dbl4 <wlo> <whi>` | double transposition in all four direction combinations |
| `crib <wlo> <whi> <minLen> <control\|x> [digits] [restarts]` | **crib dragging.** A structural test, not a statistical one: the crib's letter-repetition pattern must be reproduced exactly by the units it lands on. Run with `control` to see it recover a known plaintext from one crib |

`[digits]` is an optional ciphertext override — pass a repaired or reversed
stream to run any attack against a variant.

## How the main attack works

Substitution and transposition cannot be attacked separately: a wrong column
order makes every substitution look like noise, and vice versa. Annealing over
both at once does not converge at 196 symbols — `control` demonstrates that.

What works is exploiting the one thing a transposition cannot hide. Symbol
frequencies survive it untouched, so ranking the 18 units against English letter
frequencies gives a substitution that is approximately right. With that in hand
the column order can be solved **exactly**: Held–Karp dynamic programming over
subsets, scoring each candidate adjacency of two columns by the bigram
statistics of the letters that would end up side by side. The recovered order
gives a text, the text refines the substitution, the refined substitution gives
a better order. Two or three rounds and it converges.

For an incomplete rectangle the split of the ciphertext into columns depends on
which columns are long, so every one of the C(w, n mod w) patterns is
enumerated. That is what caps the exact method at width 15.

## What has already been run

All of the following returned nothing above −1.32, where the control's *wrong*
widths sat at −1.42 to −1.55:

- keyed columnar over whole units, widths 2–15, every long-column pattern × 250 substitution seeds
- keyed columnar over single digits, widths 3–13 — all 641,000 alternation-preserving keys
- 614 unkeyed geometries, forwards and reversed
- double transposition, one key twice, widths 2–11
- double transposition, two keys, both widths 2–7
- 6,535 real words and 1939 dates as keys × 4 readings
- irregular columnar at every width 2–13
- widths 13, 14, 15 rerun at 24,000 and 3,200 seeds after the control showed 250 was not enough
- nulls at every stride 2–9 and every offset, plus all 4,047 subsets of 1–4 null cells, plus both together
- the transposition run backwards at widths 2–30
- head-scored sweeps at N = 24, 32, 40, 50, 64, 80, 100, 130, 196 across every width
- the midpoint split: halves alone, swapped, reversed, and the anomaly deleted
- unit counts 193 to 197, and widths 28, 49, 56
- all of the above again with digit 195 repaired to 6, 7, 8, 9 and with the stream reversed

## Crib dragging — how it works, and what it needs

A crib asks whether the ciphertext *can* reproduce a known word's shape, not whether
it looks like English. THREE is T H R E E: last two equal, rest distinct. Whatever
units those five positions land on must have the same repeat pattern. That is
checkable with no substitution at all, and it cannot be overfitted.

In a columnar transposition, plaintext letter *i* sits at row *i/w* of column *i%w*,
and each column is a contiguous ciphertext block. A crib at position *p* therefore
pins one unit in each of several blocks at known row offsets. `Crib.cs` searches
block-to-column assignments depth-first, rejecting the moment the pattern breaks,
enumerates *every* consistent order rather than the first, and completes the
substitution by restarts with the crib's own letters held fixed.

Two things the control taught, both worth knowing before you spend compute:

1. **The crib must be longer than the width.** Shorter than *w* and every crib letter
   lands in a different column, so there are no intra-column constraints and about
   57% of placements survive — no discrimination at all. Longer than *w* and columns
   get revisited; survival drops to 18% and the true placement rises to the top.
2. **You must enumerate every consistent order, not the first one found.** Taking the
   first costs you the answer: the control failed at −1.56 until this was fixed, then
   returned the exact plaintext at −0.7914.

Validated: given one crib that genuinely occurs, the control returns its true
plaintext ranked first, runner-up half a point behind.

## Where to take it next

1. **A non-English corpus.** Swap `corpus.txt` for French, Latin or transliterated
   Russian and rerun everything. The code does not care what language it is given.
3. **Double transposition with two realistic keys.** Out of reach by exhaustion;
   would need a crib. A 1939 challenge cipher plausibly says something about
   itself — `SKILL`, `READER`, `CIPHER`, `DAGAPEYEFF` are all worth dragging.
4. **A non-English plaintext.** Every scorer here is English. Swap `corpus.txt`
   for French, Latin, or transliterated Russian and rerun everything; the code
   does not care what language the corpus is in.
5. **Columnar keys wider than fifteen.** The exact method is blocked by the
   long-column pattern count, but widths where 392 divides evenly — 28, 49, 56 —
   need only one pattern and the beam-search path in `Dp.cs` already handles them.
6. **A syllabary under the transposition.** Would need an n-gram model over
   syllables rather than letters. Nothing here has one yet.

## Files

| file | |
|---|---|
| `Program.cs` | language model, ciphertext, columnar primitives, mode dispatch |
| `Dp.cs` | the exact column-ordering attack (Held–Karp + beam) |
| `DigitT.cs` | digit-level transposition, exhaustive over alternation-preserving keys |
| `Routes.cs` | unkeyed geometries and the monoalphabetic solver |
| `Double.cs` | double transposition |
| `Words.cs` | word-list keys |
| `Solve.cs` | joint annealing (kept because its failure is itself a result) |
| `corpus.txt` | 147,626 letters of the book |
| `Deep.cs` | one width, many seeds, spread across every core |
| `Nulls.cs` | dummy-letter hunt, positional and symbolic |
| `Reverse.cs` | the reversed-direction model |
| `Head.cs` | head-scored sweep (priority 1) |
| `Halves.cs` | midpoint split, tail lengths, wide keys |
| `Double4.cs` | double transposition, four directions |
| `words.txt` | 6,535 candidate keys — the book's own vocabulary plus names and every 1939 date |
