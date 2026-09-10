/* D'Agapeyeff investigation — shared behaviour.
   Every block is feature-detected, so one script serves every page. */
"use strict";
const NL = String.fromCharCode(10);
const AL = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const AIDX = {}; for (let i=0;i<26;i++) AIDX[AL[i]] = i;
const ENG_RANK = "ETAOINSHRDLCUMWFGYPBVKJXQZ".split("").map(c=>AIDX[c]);

const $  = s => document.querySelector(s);
const $$ = s => Array.from(document.querySelectorAll(s));
const esc = s => String(s).replace(/[&<>]/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;"}[c]));
const counter = a => { const m=new Map(); for (const x of a) m.set(x,(m.get(x)||0)+1); return m; };
const chunk = (s,n) => { const o=[]; for(let i=0;i<s.length;i+=n) o.push(s.slice(i,i+n)); return o; };
function fmtTime(s){ s=Math.floor(s);
  if (s<60) return s+"s";
  if (s<3600) return Math.floor(s/60)+"m "+(s%60)+"s";
  return Math.floor(s/3600)+"h "+Math.floor((s%3600)/60)+"m"; }
function shuffle(a){ for(let i=a.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[a[i],a[j]]=[a[j],a[i]];} return a; }

/* ---------- language models ---------- */
let _tri=null,_bi=null;
function triTab(){ if(_tri) return _tri;
  const t=new Float32Array(17576), sp=TRI_HI-TRI_LO;
  for(let i=0;i<17576;i++) t[i]=TRI_LO+(TRI_STR.charCodeAt(i)-33)/93*sp;
  return (_tri=t); }
function biTab(){ if(_bi) return _bi;
  const t=new Float32Array(676), sp=BI_HI-BI_LO;
  for(let i=0;i<676;i++) t[i]=BI_LO+(BI_STR.charCodeAt(i)-33)/93*sp;
  return (_bi=t); }
function scoreCodes(c){ const t=triTab(), n=c.length; if(n<3) return -9;
  let x=0; for(let i=0;i+2<n;i++) x+=t[c[i]*676+c[i+1]*26+c[i+2]]; return x/(n-2); }
function scoreText(s){ const c=[]; for(const ch of s) if(AIDX[ch]!==undefined) c.push(AIDX[ch]); return scoreCodes(c); }

/* ---------- working text ---------- */
const S = { pad:3, fix:"0", text:"" };
function buildTextFull(){ const d=PRINTED.split(); const a=PRINTED.split("");
  if (S.fix==="del") a.splice(194,1); else a[194]=S.fix; return a.join(""); }
function buildText(){ let s=buildTextFull(); if (S.pad>0) s=s.slice(0,s.length-S.pad); return s; }
function unitsOf(t){ const u=[]; for(let i=0;i+1<t.length;i+=2) u.push(t.slice(i,i+2)); return u; }

function stats(text){
  const units=unitsOf(text), n=units.length, uc=counter(units);
  const rc=counter(units.map(u=>u[0])), cc=counter(units.map(u=>u[1]));
  let ic=0; if(n>1){ let s=0; for(const v of uc.values()) s+=v*(v-1); ic=s/(n*(n-1)); }
  let indep=0;
  for(const [r,rn] of rc) for(const [c,cn] of cc){
    const e=rn*cn/n, o=uc.get(r+c)||0; if(e>0) indep+=(o-e)*(o-e)/e; }
  const sorted=Array.from(uc.values()).sort((a,b)=>b-a);
  const exp=ENGF.map(f=>f/100*n); let chi=0;
  for(let i=0;i<Math.max(sorted.length,26);i++){ const o=sorted[i]||0,e=exp[i]||0; chi+=(o-e)*(o-e)/Math.max(e,0.5); }
  const breaks=[];
  for(let i=0;i<text.length;i++){ const hi=i%2===0,d=text[i];
    if(hi&&!"6789".includes(d)) breaks.push(i+1);
    if(!hi&&!"12345".includes(d)) breaks.push(i+1); }
  const reps={};
  for(const len of [2,3,4]){
    const m=new Map();
    for(let i=0;i+len<=n;i++){ const k=units.slice(i,i+len).join("");
      if(!m.has(k)) m.set(k,[]); m.get(k).push(i+1); }
    reps[len]=Array.from(m.entries()).filter(([,p])=>p.length>1).sort((a,b)=>b[1].length-a[1].length); }
  return {text,units,n,uc,rc,cc,ic,indep,chi,sorted,breaks,reps};
}

/* ---------- shared solver pieces ---------- */
function climbSigma(seq,S2,sigma,used,effort){
  const n=seq.length,L=new Array(n);
  const reb=()=>{ for(let i=0;i<n;i++) L[i]=sigma[seq[i]]; };
  reb(); let cur=scoreCodes(L), imp=true;
  while(imp){ imp=false;
    for(let a=0;a<S2;a++) for(let b=a+1;b<S2;b++){
      const t=sigma[a]; sigma[a]=sigma[b]; sigma[b]=t;
      reb(); const s=scoreCodes(L);
      if(s>cur){cur=s;imp=true;} else {const u=sigma[a];sigma[a]=sigma[b];sigma[b]=u;} }
    if(effort>=2) for(let a=0;a<S2;a++) for(let nl=0;nl<26;nl++){
      if(used[nl]) continue;
      const old=sigma[a]; sigma[a]=nl; used[old]=false; used[nl]=true;
      reb(); const s=scoreCodes(L);
      if(s>cur){cur=s;imp=true;} else {sigma[a]=old;used[nl]=false;used[old]=true;} } }
  reb(); return cur;
}
function freqOrder(seq,S2){ const c=new Array(S2).fill(0); for(const x of seq) c[x]++;
  return Array.from({length:S2},(_,i)=>i).sort((a,b)=>c[b]-c[a]); }
function seedSigma(seq,S2,sigma,used,byFreq){
  used.fill(false);
  if(byFreq){ const fo=freqOrder(seq,S2);
    for(let i=0;i<S2;i++){ sigma[fo[i]]=ENG_RANK[i]; used[ENG_RANK[i]]=true; } }
  else { const pool=shuffle(Array.from({length:26},(_,i)=>i));
    for(let i=0;i<S2;i++){ sigma[i]=pool[i]; used[pool[i]]=true; } }
}
function indexUnits(u){ const uniq=Array.from(new Set(u)); const idx=new Map(uniq.map((x,i)=>[x,i]));
  return {seq:u.map(x=>idx.get(x)),uniq,S:uniq.length}; }
function keyOrder(key){
  key=(key||"").trim();
  if(/^[\d\s,;.\-]+$/.test(key)&&/\d/.test(key)){
    const nums=key.split(/[^0-9]+/).filter(Boolean).map(Number);
    return nums.map((v,i)=>[v,i]).sort((a,b)=>a[0]-b[0]||a[1]-b[1]).map(p=>p[1]); }
  const L=key.toUpperCase().replace(/[^A-Z]/g,"").split("");
  return L.map((c,i)=>[c,i]).sort((a,b)=>a[0]<b[0]?-1:a[0]>b[0]?1:a[1]-b[1]).map(p=>p[1]);
}
function colLens(n,w){ const q=Math.floor(n/w),r=n%w,l=[]; for(let c=0;c<w;c++) l.push(c<r?q+1:q); return l; }
function applyColumnar(items,order){ const w=order.length,cols=Array.from({length:w},()=>[]);
  items.forEach((x,i)=>cols[i%w].push(x)); const o=[]; for(const c of order) o.push(...cols[c]); return o; }
function undoColumnar(items,order){
  const w=order.length,n=items.length,len=colLens(n,w),cols={}; let p=0;
  for(const c of order){ cols[c]=items.slice(p,p+len[c]); p+=len[c]; }
  const ptr=new Array(w).fill(0),o=[];
  for(let i=0;i<n;i++){ const c=i%w; o.push(cols[c][ptr[c]++]); } return o;
}

/* ============================ NAVIGATION ============================ */
function initNav(){
  const side=$("#side"), tog=$("#navtoggle"), scrim=$("#scrim"), close=$("#navclose");
  const set=o=>{ if(!side) return;
    side.classList.toggle("open",o); if(scrim) scrim.classList.toggle("on",o);
    if(tog) tog.setAttribute("aria-expanded",o?"true":"false"); };
  if(tog) tog.addEventListener("click",()=>set(!side.classList.contains("open")));
  if(close) close.addEventListener("click",()=>set(false));
  if(scrim) scrim.addEventListener("click",()=>set(false));
  document.addEventListener("keydown",e=>{ if(e.key==="Escape") set(false); });
  const page=document.body.dataset.page;
  $$("#side .navlist > li > a").forEach(a=>{
    if((a.getAttribute("href")||"")===page) a.classList.add("on"); });
  $$("#side a").forEach(a=>{
    if((a.getAttribute("href")||"").startsWith("#")) a.addEventListener("click",()=>set(false)); });
  /* in-page anchors get a scrollspy where they exist */
  const subs=$$("#side .navsub a[href^='#']").map(a=>{
    const el=document.getElementById(a.getAttribute("href").slice(1));
    return el?{a,el}:null; }).filter(Boolean);
  if(subs.length){
    const upd=()=>{ const y=scrollY+130; let cur=null;
      for(const s of subs){ if(s.el.getBoundingClientRect().top+scrollY<=y) cur=s; }
      subs.forEach(s=>s.a.classList.toggle("on",s===cur)); };
    addEventListener("scroll",upd,{passive:true}); upd();
  }
  const meta=$("#sidestat");
  if(meta){ const st=stats(buildText());
    meta.innerHTML='<span class="statusdot"></span>UNSOLVED<br><b>'+st.n+'</b> units &middot; <b>'
      +st.uc.size+'</b> distinct<br><b>222M</b> keys rejected'; }
}

/* ============================ PAGE: cryptogram ============================ */
function initViews(){
  const out=$("#viewout"); if(!out) return;
  let VIEW="printed";
  const render=()=>{
    const st=stats(S.text), note=$("#viewnote"); let html="",n="";
    if(VIEW==="printed"){
      const g=chunk(buildTextFull(),5),lines=[];
      for(let i=0;i<g.length;i+=10) lines.push(g.slice(i,i+10).join(" "));
      html=esc(lines.join(NL));
      n="The 79 groups exactly as set on page 159. The five-digit grouping is a transmission convention, not part of the cipher.";
    } else if(VIEW==="stream"){
      html=esc(chunk(S.text,70).join(NL));
      n=S.text.length+" working digits, continuous. This is what every tool operates on.";
    } else if(VIEW==="units"){
      const r=[]; for(let i=0;i<st.units.length;i+=25) r.push(st.units.slice(i,i+25).join(" "));
      html=esc(r.join(NL)); n=st.n+" two-digit units, "+st.uc.size+" of them distinct.";
    } else if(VIEW==="square"){
      const w=Math.round(Math.sqrt(st.n)),r=[];
      for(let i=0;i<st.units.length;i+=w) r.push(st.units.slice(i,i+w).join(" "));
      html=esc(r.join(NL));
      n=st.n+" units laid out "+w+" × "+Math.ceil(st.n/w)+". At the printed length this is a perfect 14 × 14.";
    } else {
      const cells=[];
      for(let i=0;i<S.text.length;i++){
        const d=S.text[i],hi=i%2===0;
        const ok=hi?"6789".includes(d):"12345".includes(d);
        cells.push('<span class="'+(ok?(hi?"hi":"lo"):"bad")+'" title="digit '+(i+1)+'">'+d+"</span>"); }
      out.innerHTML='<div class="pmap">'+cells.join("")+"</div>";
      if(note) note.textContent=st.breaks.length
        ? "Alternation broken at digit "+st.breaks.join(", ")+". Row digits shaded, column digits plain."
        : "Alternation intact across all "+S.text.length+" digits.";
      return; }
    out.innerHTML=html; if(note) note.textContent=n;
  };
  $$("#viewseg button").forEach(b=>b.addEventListener("click",()=>{
    $$("#viewseg button").forEach(x=>x.classList.remove("on"));
    b.classList.add("on"); VIEW=b.dataset.view; render(); }));
  const cp=$("#copybtn");
  if(cp) cp.addEventListener("click",()=>{
    if(navigator.clipboard) navigator.clipboard.writeText(out.innerText);
    cp.textContent="copied"; setTimeout(()=>cp.textContent="copy view",1300); });
  window.__renderView=render; render();
}

/* ============================ PAGE: measurements ============================ */
function bar(label,val,max,right){
  return '<div class="k">'+esc(label)+'</div><div class="bar"><i style="width:'
    +(max?100*val/max:0).toFixed(1)+'%"></i></div><div class="v">'+esc(right!==undefined?right:val)+"</div>"; }
function initMeasure(){
  if(!$("#readout")) return;
  const render=()=>{
    const st=stats(S.text);
    $("#readout").innerHTML=[
      ["digits",st.text.length,"","395 printed"],
      ["units",st.n,"","196 = 14 × 14"],
      ["distinct",st.uc.size,st.uc.size<20?"warn":"","English ~22"],
      ["ind. of coincidence",st.ic.toFixed(4),(st.ic>0.06&&st.ic<0.075)?"ok":"warn","English 0.0667"],
      ["row/col χ²",st.indep.toFixed(1),st.indep>30?"bad":"","independent ~16"],
      ["fit to English χ²",st.chi.toFixed(1),st.chi>20?"bad":st.chi>12?"warn":"ok","English ~7.5"],
      ["parity breaks",st.breaks.length,st.breaks.length?"bad":"ok",
        st.breaks.length?"at digit "+st.breaks.slice(0,3).join(", "):"perfect"]
    ].map(([k,v,c,r])=>'<div class="cell"><span class="k">'+k+'</span><span class="v '+(c||"")+'">'+v
      +'</span><span class="ref">'+r+"</span></div>").join("");

    const dc=counter(st.text.split("")),dmax=Math.max(...dc.values());
    if($("#digitbars")) $("#digitbars").innerHTML="0123456789".split("")
      .map(d=>bar(d,dc.get(d)||0,dmax,dc.get(d)||0)).join("");
    const rmax=Math.max(...st.rc.values()),cmax=Math.max(...st.cc.values());
    if($("#rowbars")) $("#rowbars").innerHTML='<div class="k" style="grid-column:1/-1;color:var(--acc)">first digit · row</div>'
      +Array.from(st.rc.keys()).sort().map(d=>bar(d,st.rc.get(d),rmax,st.rc.get(d))).join("");
    if($("#colbars")) $("#colbars").innerHTML='<div class="k" style="grid-column:1/-1;color:var(--cy)">second digit · column</div>'
      +Array.from(st.cc.keys()).sort().map(d=>bar(d,st.cc.get(d),cmax,st.cc.get(d))).join("");

    if($("#heat")){
      const rows=Array.from(st.rc.keys()).sort(),cols=Array.from(st.cc.keys()).sort();
      let h="<tr><th></th>"+cols.map(c=>"<th>"+c+"</th>").join("")+"<th>Σ</th></tr>";
      const vmax=Math.max(...Array.from(st.uc.values()));
      for(const r of rows){ h+="<tr><th>"+r+"</th>";
        for(const c of cols){ const o=st.uc.get(r+c)||0,e=st.rc.get(r)*st.cc.get(c)/st.n;
          h+='<td style="background:color-mix(in srgb,var(--acc) '+((vmax?o/vmax:0)*22).toFixed(0)
            +'%,transparent)">'+(o||"·")+"<small>"+e.toFixed(1)+"</small></td>"; }
        h+="<th>"+st.rc.get(r)+"</th></tr>"; }
      h+="<tr><th>Σ</th>"+cols.map(c=>"<th>"+st.cc.get(c)+"</th>").join("")+"<th>"+st.n+"</th></tr>";
      $("#heat").innerHTML=h; }

    if($("#unitbars")){
      const pairs=Array.from(st.uc.entries()).sort((a,b)=>b[1]-a[1]);
      const pmax=Math.max(pairs[0][1],ENGF[0]/100*st.n);
      $("#unitbars").innerHTML=pairs.map((p,i)=>{
        const e=(ENGF[i]||0)/100*st.n;
        return '<div class="k">'+p[0]+'</div><div><div class="bar"><i style="width:'
          +(100*p[1]/pmax).toFixed(1)+'%"></i></div><div class="bar" style="height:3px;margin-top:2px"><i style="width:'
          +(100*e/pmax).toFixed(1)+'%;background:var(--mut)"></i></div></div><div class="v">'
          +p[1]+' <span style="color:var(--mut)">/ '+e.toFixed(1)+"</span></div>"; }).join(""); }

    if($("#repeats")){
      const r2=st.reps[2],r3=st.reps[3],r4=st.reps[4];
      const top=r3.slice(0,6).map(([k,p])=>'<tr><td class="mono">'+chunk(k,2).join(" ")
        +'</td><td class="n">'+p.length+'</td><td class="mono" style="color:var(--mut)">'+p.join(", ")+"</td></tr>").join("");
      $("#repeats").innerHTML='<div class="readout" style="margin-bottom:10px">'
        +'<div class="cell"><span class="k">pairs</span><span class="v">'+r2.length+'</span><span class="ref">English ~43</span></div>'
        +'<div class="cell"><span class="k">triples</span><span class="v '+(r3.length<12?"bad":"")+'">'+r3.length+'</span><span class="ref">English ~20</span></div>'
        +'<div class="cell"><span class="k">quads</span><span class="v '+(r4.length?"":"bad")+'">'+r4.length+'</span><span class="ref">English ~10</span></div></div>'
        +(top?'<table><tr><th>triple</th><th class="n">×</th><th>at unit</th></tr>'+top+"</table>"
             :'<p class="hint">No unit triple occurs more than once.</p>'); }
  };
  window.__renderMeasure=render; render();
}

/* ============================ boot ============================ */
document.addEventListener("DOMContentLoaded",()=>{
  S.text=buildText();
  initNav(); initViews(); initMeasure();
  if (window.initWorkbench) window.initWorkbench();
});
