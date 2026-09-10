import os, re, random, statistics
from collections import Counter
HERE=os.path.dirname(os.path.abspath(__file__))
txt=re.sub(r'[^A-Z]','',open(os.path.join(HERE,"corpus.txt"),encoding='utf-8',errors='replace').read().upper()).replace("J","I")

def reps(seq,n=3):
    c=Counter(tuple(seq[i:i+n]) for i in range(len(seq)-n+1))
    return sum(1 for v in c.values() if v>1)
def score(seq):           # language-free, substitution-free
    return reps(seq,2)+3*reps(seq,3)+9*reps(seq,4)

def applyCol(items,order):
    w=len(order); cols=[[] for _ in range(w)]
    for i,x in enumerate(items): cols[i%w].append(x)
    return [x for c in order for x in cols[c]]
def undoCol(items,order):
    w=len(order); n=len(items); q,r=divmod(n,w)
    ln=[q+1 if c<r else q for c in range(w)]
    cols={}; p=0
    for c in order: cols[c]=items[p:p+ln[c]]; p+=ln[c]
    ptr=[0]*w; out=[]
    for i in range(n):
        c=i%w; out.append(cols[c][ptr[c]]); ptr[c]+=1
    return out

rnd=random.Random(11)
print("Can a candidate key be recognised WITHOUT guessing the substitution")
print("and WITHOUT assuming the language?  Objective = weighted count of recurring")
print("unit pairs, triples and quads. Both invariant under any substitution.\n")

for w in (9,11,13):
    hits=[]
    for trial in range(40):
        s=rnd.randrange(0,len(txt)-260)
        plain=list(txt[s:s+196])
        true=list(range(w)); rnd.shuffle(true)
        ct=applyCol(plain,true)
        good=score(undoCol(ct,true))
        bad=[]
        for _ in range(3000):
            o=list(range(w)); rnd.shuffle(o)
            if o==true: continue
            bad.append(score(undoCol(ct,o)))
        mu=statistics.mean(bad); sd=statistics.pstdev(bad) or 1
        hits.append(((good-mu)/sd, good, mu, sd, sum(1 for b in bad if b>=good)))
    z=[h[0] for h in hits]
    beat=[h[4] for h in hits]
    print(f"width {w:>2}:  true key scores {statistics.mean([h[1] for h in hits]):6.1f}   "
          f"random keys {statistics.mean([h[2] for h in hits]):6.1f} +/- {statistics.mean([h[3] for h in hits]):4.1f}")
    print(f"           separation {statistics.mean(z):5.1f} sigma   "
          f"random keys beating the true key: {statistics.mean(beat):.1f} in 3000  "
          f"({100*statistics.mean(beat)/3000:.3f}%)")
