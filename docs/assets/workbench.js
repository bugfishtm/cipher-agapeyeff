/* Workbench tools. Loaded only on 10-workbench.html; relies on app.js + data.js. */
"use strict";

/* ===================== tool 2 : the square ===================== */
const SQ = { rows: "67890".split(""), cols: "12345".split(""), cells: [], order: "rc" };
function alphabetFor(mode){
  if (mode === "q") return "ABCDEFGHIJKLMNOPRSTUVWXYZ";
  if (mode === "full") return "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
  return "ABCDEFGHIKLMNOPQRSTUVWXYZ";
}
function fillKeyword(kw, mode){
  const alpha = alphabetFor(mode), seen = new Set(), out = [];
  for (const ch of (kw||"").toUpperCase().replace(/[^A-Z]/g,"")){
    let c = ch;
    if (mode === "ij" && c === "J") c = "I";
    if (!alpha.includes(c) || seen.has(c)) continue;
    seen.add(c); out.push(c);
  }
  for (const c of alpha) if (!seen.has(c)){ seen.add(c); out.push(c); }
  SQ.cells = [];
  for (let r=0;r<5;r++) SQ.cells.push(out.slice(r*5,r*5+5));
  if (out.length > 25) SQ.cells.push(out.slice(25));
}
function renderSquare(){
  let h = "<tr><th></th>" + SQ.cols.map((c,i)=>'<th><input data-lab="c" data-i="'+i+'" value="'+esc(c)
        +'" maxlength="1" style="width:34px;color:var(--coord);font-weight:700"></th>').join("") + "</tr>";
  SQ.cells.forEach((row,ri)=>{
    h += '<tr><th><input data-lab="r" data-i="'+ri+'" value="'+esc(SQ.rows[ri]||"")
       + '" maxlength="1" style="width:34px;color:var(--coord);font-weight:700"></th>'
       + row.map((v,ci)=>'<td><input data-r="'+ri+'" data-c="'+ci+'" value="'+esc(v)+'" maxlength="1"></td>').join("")
       + "</tr>";
  });
  $("#sqtable").innerHTML = h;
  $$("#sqtable input").forEach(inp => inp.addEventListener("input", e=>{
    const t = e.target, v = (t.value||"").toUpperCase().slice(0,1);
    t.value = v;
    if (t.dataset.lab === "r") SQ.rows[+t.dataset.i] = v;
    else if (t.dataset.lab === "c") SQ.cols[+t.dataset.i] = v;
    else SQ.cells[+t.dataset.r][+t.dataset.c] = v;
    decodeSquare();
  }));
  $("#coordnote").textContent = SQ.order === "rc"
    ? "Units are read as row then column — 75 means row 7, column 5."
    : "Units are read as column then row — 75 means column 7, row 5.";
}
function squareMap(){
  const m = new Map();
  SQ.cells.forEach((row,ri)=> row.forEach((v,ci)=>{
    const r = SQ.rows[ri], c = SQ.cols[ci];
    if (!r || !c || !v) return;
    m.set(SQ.order === "rc" ? r+c : c+r, v);
  }));
  return m;
}
function decodeSquare(){
  const m = squareMap(), units = unitsOf(S.text);
  let plain = "", miss = 0;
  for (const u of units){ const v = m.get(u); if (v) plain += v; else { plain += "·"; miss++; } }
  const sc = scoreText(plain);
  $("#sqout").textContent = chunk(plain,5).join(" ").replace(/((\S+\s){10})/g,"$1"+NL);
  $("#sqscore").textContent = isFinite(sc) ? sc.toFixed(3) : "—";
  $("#sqgauge").style.width = Math.max(0, Math.min(100,(sc-REF_NOISE)/(REF_ENGLISH-REF_NOISE)*100)).toFixed(1)+"%";
  let v;
  if (miss > units.length/2) v = "unmapped";
  else if (sc > -3.55) v = "English";
  else if (sc > -3.80) v = "English-ish";
  else if (sc > -4.10) v = "not English";
  else v = "noise";
  const el = $("#sqverdict");
  el.textContent = v + (miss ? "  ("+miss+" unmapped)" : "");
  el.className = "v sm " + (v==="English" ? "ok" : v==="English-ish" ? "warn" : "");
}

/* ===================== tool 3 : substitution solver ===================== */
const SOLVER = { running:false, best:-99, bestKey:null, uniq:null, seq:null, S:0,
                 restarts:0, t0:0, stuck:0, bestAt:0 };
function solverSequence(){
  const units = unitsOf(S.text);
  const target = $("#solvetarget").value;
  if (target === "cipher") return units;
  if (target === "shuffle") return shuffle(units.slice());
  const txt = ("THEGENERALORDEREDTHESECONDBRIGADETOATTACKATTHREETHIRTYINTHEMORNINGTHETHIRD"
             + "BRIGADEONEHOURLATERONTHELEFTTHEFOURTHTOKEEPINRESERVEANDTOADVANCEONLYWHEN"
             + "THESIGNALISGIVENFROMHEADQUARTERS").slice(0, units.length);
  const perm = shuffle(AL.split(""));
  return txt.split("").map(c => "u"+perm[AIDX[c]]);
}
function startSolver(){
  const seq0 = solverSequence();
  const ix = indexUnits(seq0);
  Object.assign(SOLVER, { seq: ix.seq, uniq: ix.uniq, S: ix.S,
    running:true, best:-99, bestKey:null, restarts:0, stuck:0, t0:performance.now(), bestAt:0 });
  $("#solvebtn").disabled = true; $("#stopbtn").disabled = false;
  $("#solveverdict").textContent = "running";
  solverStep();
}
function solverStep(){
  if (!SOLVER.running) return;
  const limit = +$("#solvestop").value;
  const patience = +$("#solvepatience").value;
  const effort = +$("#solveeffort").value;
  const t0 = performance.now();
  const sigma = new Array(SOLVER.S), used = new Array(26).fill(false);

  while (performance.now() - t0 < 70){
    if (SOLVER.bestKey && SOLVER.stuck >= patience){
      for (let i=0;i<SOLVER.S;i++) sigma[i] = SOLVER.bestKey[i];
      used.fill(false); for (const v of sigma) used[v] = true;
      const kicks = 2 + Math.floor(Math.random()*3);
      for (let k=0;k<kicks;k++){
        const a = Math.floor(Math.random()*SOLVER.S), b = Math.floor(Math.random()*SOLVER.S);
        const t = sigma[a]; sigma[a]=sigma[b]; sigma[b]=t;
      }
      SOLVER.stuck = 0;
    } else {
      seedSigma(SOLVER.seq, SOLVER.S, sigma, used, SOLVER.restarts===0, null);
    }
    if (effort >= 3){
      const a = Math.floor(Math.random()*SOLVER.S), b = Math.floor(Math.random()*SOLVER.S);
      const t = sigma[a]; sigma[a]=sigma[b]; sigma[b]=t;
    }
    const sc = climbSigma(SOLVER.seq, SOLVER.S, sigma, used, effort, null);
    SOLVER.restarts++;
    if (sc > SOLVER.best){
      SOLVER.best = sc; SOLVER.bestKey = sigma.slice(); SOLVER.stuck = 0;
      SOLVER.bestAt = (performance.now()-SOLVER.t0)/1000;
    } else SOLVER.stuck++;
  }
  paintSolver();
  const elapsed = (performance.now()-SOLVER.t0)/1000;
  if (limit > 0 && elapsed >= limit){ stopSolver(); return; }
  setTimeout(solverStep, 0);
}
function paintSolver(){
  const elapsed = (performance.now()-SOLVER.t0)/1000;
  $("#solvescore").textContent = SOLVER.best.toFixed(3);
  $("#solveiter").textContent = SOLVER.restarts.toLocaleString();
  $("#solveelapsed").textContent = fmtTime(elapsed);
  $("#solvebestat").textContent = SOLVER.bestKey ? "found at " + fmtTime(SOLVER.bestAt) : "";
  $("#solverate").textContent = elapsed>0
    ? Math.round(SOLVER.restarts/elapsed).toLocaleString() + " restarts/sec" : "";
  $("#solvegauge").style.width =
    Math.max(0,Math.min(100,(SOLVER.best-REF_NOISE)/(REF_ENGLISH-REF_NOISE)*100)).toFixed(1)+"%";
  if (SOLVER.bestKey){
    const txt = SOLVER.seq.map(i=>AL[SOLVER.bestKey[i]]).join("");
    $("#solveout").textContent = chunk(txt,5).join(" ").replace(/((\S+\s){10})/g,"$1"+NL);
  }
  const el = $("#solveverdict");
  let v, cls="";
  if (SOLVER.best > -3.55){ v = "solved"; cls = "ok"; }
  else if (SOLVER.best > -3.80){ v = "close"; cls = "warn"; }
  else { v = "no better than noise"; cls = "bad"; }
  el.textContent = v; el.className = "v sm " + cls;
}
function stopSolver(){
  SOLVER.running = false;
  $("#solvebtn").disabled = false; $("#stopbtn").disabled = true;
}

/* ===================== tool 4 : transposition lab ===================== */
let LAST_ROUTE = null;
function routeMap(kind, param, n){
  const m = new Array(n).fill(0);
  if (kind === "none"){ for (let i=0;i<n;i++) m[i]=i; return m; }
  if (kind === "rail"){
    const k = Math.max(2, Math.min(n-1, param));
    const rows = Array.from({length:k},()=>[]);
    let r = 0, dir = 1;
    for (let i=0;i<n;i++){ rows[r].push(i); if (r===0) dir=1; else if (r===k-1) dir=-1; r+=dir; }
    let p = 0;
    for (const row of rows) for (const i of row) m[i] = p++;
    return m;
  }
  if (kind === "chinese"){
    const w = param, h = Math.ceil(n/w);
    if (w*h - n >= w) return null;
    const cell = new Array(n); let k = 0;
    for (let j=0;j<w;j++){
      const c = w-1-j, down = (j%2===0);
      for (let t=0;t<h;t++){
        const row = down ? t : h-1-t, idx = row*w + c;
        if (idx >= n) continue;
        if (k >= n) return null;
        cell[k++] = idx;
      }
    }
    if (k !== n) return null;
    for (let i=0;i<n;i++) m[i] = cell[i];
    return m;
  }
  if (kind === "spiral" || kind === "diag"){
    const w = param;
    if (n % w !== 0) return null;
    const h = n/w, cells = [];
    if (kind === "spiral"){
      let top=0, bot=h-1, left=0, right=w-1;
      while (top<=bot && left<=right){
        for (let c=left;c<=right;c++) cells.push(top*w+c); top++;
        for (let r2=top;r2<=bot;r2++) cells.push(r2*w+right); right--;
        if (top<=bot){ for (let c=right;c>=left;c--) cells.push(bot*w+c); bot--; }
        if (left<=right){ for (let r2=bot;r2>=top;r2--) cells.push(r2*w+left); left++; }
      }
    } else {
      for (let s2=0;s2<w+h-1;s2++)
        for (let r2=0;r2<h;r2++){ const c = s2-r2; if (c>=0 && c<w) cells.push(r2*w+c); }
    }
    if (cells.length !== n) return null;
    for (let p=0;p<n;p++) m[cells[p]] = p;
    return m;
  }
  const w = Math.max(2, Math.min(n, param));
  const len = colLens(n,w), st = [0];
  for (let c=1;c<w;c++) st.push(st[c-1] + len[c-1]);
  for (let i=0;i<n;i++){
    const c = i % w, row = Math.floor(i / w);
    if (kind === "columnar") m[i] = st[c] + row;
    else if (kind === "boustro") m[i] = st[c] + (c % 2 === 0 ? row : len[c]-1-row);
    else { if (n % w !== 0) return null; m[i] = st[w-1-c] + row; }
  }
  return m;
}
function showTransposed(text, label){
  const st2 = stats(text), base = stats(S.text).breaks.length;
  $("#torder").textContent = label;
  const pEl = $("#tparity");
  if (st2.breaks.length <= base){
    pEl.textContent = base ? "intact (" + base + " pre-existing)" : "intact";
    pEl.className = "v sm ok";
  } else { pEl.textContent = "broken × " + st2.breaks.length; pEl.className = "v sm bad"; }
  $("#tdistinct").textContent = st2.uc.size;
  $("#tchi").textContent = st2.chi.toFixed(1);
  $("#tchi").className = "v " + (st2.chi>20?"bad":st2.chi>12?"warn":"ok");
  $("#tout").textContent = chunk(text,70).join(NL);
}
function runTranspose(push){
  const order = keyOrder($("#tkey").value);
  if (order.length < 2){ $("#tout").textContent = "Give a key of at least two letters or numbers."; return; }
  const mode = $("#tunit").value, dir = $("#tdir").value;
  const items = mode === "unit" ? unitsOf(S.text) : S.text.split("");
  const text = (dir === "undo" ? undoColumnar(items, order) : applyColumnar(items, order)).join("");
  showTransposed(text, "width " + order.length + ", columns read " + order.map(x=>x+1).join(" "));
  LAST_ROUTE = null;
  if (push){ S.text = text; $("#worktext").value = chunk(text,70).join(NL); refresh(); }
}
function runRoute(){
  const kind = $("#troute").value, param = parseInt($("#trw").value,10) || 14;
  const units = unitsOf(S.text), n = units.length;
  let m = routeMap(kind, param, n);
  if (!m){
    $("#tout").textContent = "That route needs a width that fits " + n
      + " exactly. Try 2, 4, 7, 14, 28, 49 or 98.";
    return;
  }
  if ($("#trrev").checked){ const rv = new Array(n); for (let i=0;i<n;i++) rv[i] = m[n-1-i]; m = rv; }
  const text = m.map(i=>units[i]).join("");
  showTransposed(text, kind + (kind==="none"?"":" · "+param) + ($("#trrev").checked?" · reversed":""));
  LAST_ROUTE = text;
}

/* ===================== tool 5 : joint search ===================== */
const JOINT = { running:false, best:-99, bestTxt:"", bestW:0, tries:0, t0:0, bestAt:0 };

function greedyOrder(blocks, posLen, sigma){
  const w = blocks.length, bi = biTab();
  const adj = new Float64Array(w*w);
  for (let a=0;a<w;a++) for (let b=0;b<w;b++){
    if (a===b) continue;
    const m = Math.min(blocks[a].length, blocks[b].length);
    let s2 = 0;
    for (let i=0;i<m;i++) s2 += bi[sigma[blocks[a][i]]*26 + sigma[blocks[b][i]]];
    adj[a*w+b] = s2;
  }
  const fits = (b,p) => posLen === null || blocks[b].length === posLen[p];
  let bestOrd = null, bestVal = -Infinity;
  for (let start=0;start<w;start++){
    if (!fits(start,0)) continue;
    const used = new Array(w).fill(false);
    const ord = [start]; used[start] = true;
    let val = 0, ok = true;
    for (let p=1;p<w;p++){
      let pick = -1, pv = -Infinity;
      for (let b=0;b<w;b++){
        if (used[b] || !fits(b,p)) continue;
        const v = adj[ord[p-1]*w+b];
        if (v > pv){ pv = v; pick = b; }
      }
      if (pick < 0){ ok = false; break; }
      ord.push(pick); used[pick] = true; val += pv;
    }
    if (ok && val > bestVal){ bestVal = val; bestOrd = ord; }
  }
  if (!bestOrd) return null;
  /* 2-opt on positions */
  let improved = true;
  const val = o => { let v=0; for (let p=0;p+1<w;p++) v += adj[o[p]*w+o[p+1]]; return v; };
  let cur = val(bestOrd);
  while (improved){
    improved = false;
    for (let i=0;i<w;i++) for (let j=i+1;j<w;j++){
      if (posLen && blocks[bestOrd[i]].length !== posLen[j]) continue;
      if (posLen && blocks[bestOrd[j]].length !== posLen[i]) continue;
      const t = bestOrd[i]; bestOrd[i]=bestOrd[j]; bestOrd[j]=t;
      const v = val(bestOrd);
      if (v > cur){ cur = v; improved = true; }
      else { const u = bestOrd[i]; bestOrd[i]=bestOrd[j]; bestOrd[j]=u; }
    }
  }
  return bestOrd;
}
function assemble(blocks, order, n){
  const w = order.length, out = new Array(n);
  let idx = 0, rows = 0;
  for (const b of blocks) rows = Math.max(rows, b.length);
  for (let row=0; row<rows; row++)
    for (let p=0;p<w;p++){
      const b = blocks[order[p]];
      if (row < b.length) out[idx++] = b[row];
    }
  return out;
}
function jointAttempt(seq, S2, w, anyPattern){
  const n = seq.length, q = Math.floor(n/w), r = n%w;
  const posLen = anyPattern ? null : Array.from({length:w},(_,p)=> p<r ? q+1 : q);
  /* choose which ciphertext blocks are long */
  const longIdx = shuffle(Array.from({length:w},(_,i)=>i)).slice(0,r);
  const isLong = new Array(w).fill(false); longIdx.forEach(i=>isLong[i]=true);
  const blocks = []; let p = 0;
  for (let k=0;k<w;k++){ const L = isLong[k] ? q+1 : q; blocks.push(seq.slice(p,p+L)); p += L; }

  const sigma = new Array(S2), used = new Array(26).fill(false);
  seedSigma(seq, S2, sigma, used, Math.random()<0.5, null);

  let best = -99, bestTxt = "";
  for (let it=0; it<5; it++){
    const ord = greedyOrder(blocks, posLen, sigma);
    if (!ord) break;
    const syms = assemble(blocks, ord, n);
    const sc = climbSigma(syms, S2, sigma, used, 2, null);
    if (sc > best){
      best = sc;
      bestTxt = syms.map(i=>AL[sigma[i]]).join("");
    } else break;
  }
  return { score: best, text: bestTxt };
}
function startJoint(){
  Object.assign(JOINT, { running:true, best:-99, bestTxt:"", bestW:0, tries:0,
                         t0:performance.now(), bestAt:0 });
  $("#jrun").disabled = true; $("#jstopbtn").disabled = false;
  $("#jverdict").textContent = "running";
  jointStep();
}
function jointStep(){
  if (!JOINT.running) return;
  const limit = +$("#jstop").value;
  const anyPattern = $("#jpattern").value === "any";
  const wsel = +$("#jwidth").value;
  let units = unitsOf(S.text);
  if ($("#jtarget").value === "control"){
    const txt = ("THEGENERALORDEREDTHESECONDBRIGADETOATTACKATTHREETHIRTYINTHEMORNINGTHETHIRD"
               + "BRIGADEONEHOURLATERONTHELEFTTHEFOURTHTOKEEPINRESERVEANDTOADVANCEONLYWHEN"
               + "THESIGNALISGIVENFROMHEADQUARTERS").slice(0, units.length);
    const perm = shuffle(AL.split(""));
    const plain = txt.split("").map(c => "u"+perm[AIDX[c]]);
    const ord = shuffle(Array.from({length:11},(_,i)=>i));
    units = applyColumnar(plain, ord);
  }
  const ix = indexUnits(units);
  const t0 = performance.now();
  while (performance.now() - t0 < 80){
    const w = wsel > 0 ? wsel : 2 + Math.floor(Math.random()*14);
    const res = jointAttempt(ix.seq, ix.S, w, anyPattern);
    JOINT.tries++;
    if (res.score > JOINT.best){
      JOINT.best = res.score; JOINT.bestTxt = res.text; JOINT.bestW = w;
      JOINT.bestAt = (performance.now()-JOINT.t0)/1000;
    }
  }
  paintJoint();
  const elapsed = (performance.now()-JOINT.t0)/1000;
  if (limit > 0 && elapsed >= limit){ stopJoint(); return; }
  setTimeout(jointStep, 0);
}
function paintJoint(){
  const elapsed = (performance.now()-JOINT.t0)/1000;
  $("#jscore").textContent = JOINT.best.toFixed(3);
  $("#jtries").textContent = JOINT.tries.toLocaleString();
  $("#jelapsed").textContent = fmtTime(elapsed);
  $("#jat").textContent = JOINT.bestW ? "width " + JOINT.bestW + ", at " + fmtTime(JOINT.bestAt) : "";
  $("#jrate").textContent = elapsed>0
    ? Math.round(JOINT.tries/elapsed).toLocaleString() + " attempts/sec" : "";
  $("#jgauge").style.width =
    Math.max(0,Math.min(100,(JOINT.best-REF_NOISE)/(REF_ENGLISH-REF_NOISE)*100)).toFixed(1)+"%";
  if (JOINT.bestTxt) $("#jout").textContent = chunk(JOINT.bestTxt,5).join(" ").replace(/((\S+\s){10})/g,"$1"+NL);
  const el = $("#jverdict");
  let v, cls="";
  /* this tool takes the best of thousands of (width, pattern, seed) combinations,
     so its own floor on a message with nothing in it sits near -3.70, not -3.85 */
  if (JOINT.best > -3.45){ v = "solved"; cls = "ok"; }
  else if (JOINT.best > -3.62){ v = "worth a look"; cls = "warn"; }
  else { v = "search noise"; cls = "bad"; }
  el.textContent = v; el.className = "v sm " + cls;
}
function stopJoint(){
  JOINT.running = false;
  $("#jrun").disabled = false; $("#jstopbtn").disabled = true;
}


/* ===================== wiring ===================== */
function refresh(){
  if (window.__renderView) window.__renderView();
  if (window.__renderMeasure) window.__renderMeasure();
  decodeSquare();
  const ws=$("#workstatus");
  if (ws) ws.textContent = S.text.length + " digits / " + Math.floor(S.text.length/2) + " units";
}
function setWorkingFromControls(){
  S.text = buildText();
  $("#worktext").value = chunk(S.text,70).join(NL);
  refresh();
}
window.initWorkbench = function(){
  if (!$("#sqtable")) return;

  fillKeyword("", "ij"); renderSquare();
  $("#fillkw").addEventListener("click", ()=>{ fillKeyword($("#kw").value, $("#alphamode").value); renderSquare(); decodeSquare(); });
  $("#fillplain").addEventListener("click", ()=>{ fillKeyword("", $("#alphamode").value); renderSquare(); decodeSquare(); });
  $("#transposesq").addEventListener("click", ()=>{
    const g=SQ.cells,t=[];
    for(let c=0;c<5;c++){ const row=[]; for(let r=0;r<5;r++) row.push((g[r]||[])[c]||""); t.push(row); }
    SQ.cells=t; renderSquare(); decodeSquare(); });
  $("#swapcoord").addEventListener("click", ()=>{ SQ.order = SQ.order==="rc"?"cr":"rc"; renderSquare(); decodeSquare(); });
  $("#kw").addEventListener("keydown", e=>{ if(e.key==="Enter") $("#fillkw").click(); });

  $$("#padseg button").forEach(b=>b.addEventListener("click",()=>{
    $$("#padseg button").forEach(x=>x.classList.remove("on"));
    b.classList.add("on"); S.pad=+b.dataset.pad; setWorkingFromControls(); }));
  $$("#fixseg button").forEach(b=>b.addEventListener("click",()=>{
    $$("#fixseg button").forEach(x=>x.classList.remove("on"));
    b.classList.add("on"); S.fix=b.dataset.fix; setWorkingFromControls(); }));
  $("#applytext").addEventListener("click",()=>{
    const v=$("#worktext").value.replace(/[^0-9]/g,"");
    if(v.length<4){ $("#workstatus").textContent="too short"; return; }
    S.text=v; refresh(); });
  $("#resettext").addEventListener("click",()=>{
    S.pad=3; S.fix="0";
    $$("#padseg button").forEach((x,i)=>x.classList.toggle("on",i===0));
    $$("#fixseg button").forEach((x,i)=>x.classList.toggle("on",i===0));
    setWorkingFromControls(); });

  $("#solvebtn").addEventListener("click", startSolver);
  $("#stopbtn").addEventListener("click", stopSolver);
  $("#pushsq").addEventListener("click", ()=>{
    if(!SOLVER.bestKey || $("#solvetarget").value!=="cipher"){
      $("#pushsq").textContent="run it on the cipher first";
      setTimeout(()=>{ $("#pushsq").textContent="send this key to the square"; },1800); return; }
    SQ.cells=[]; for(let r=0;r<5;r++) SQ.cells.push(["","","","",""]);
    SOLVER.uniq.forEach((u,i)=>{
      const ri=SQ.rows.indexOf(u[0]), ci=SQ.cols.indexOf(u[1]);
      if(ri>=0&&ci>=0&&ri<5) SQ.cells[ri][ci]=AL[SOLVER.bestKey[i]]; });
    renderSquare(); decodeSquare();
    $("#t2").scrollIntoView({behavior:"smooth",block:"start"}); });

  $("#trun").addEventListener("click", ()=>runTranspose(false));
  $("#tpush").addEventListener("click", ()=>{
    if(LAST_ROUTE){ S.text=LAST_ROUTE; $("#worktext").value=chunk(S.text,70).join(NL); refresh(); return; }
    runTranspose(true); });
  $("#tkey").addEventListener("keydown", e=>{ if(e.key==="Enter") runTranspose(false); });
  $("#trroute").addEventListener("click", runRoute);
  $("#troute").addEventListener("change", runRoute);
  $("#trw").addEventListener("keydown", e=>{ if(e.key==="Enter") runRoute(); });

  $("#jrun").addEventListener("click", startJoint);
  $("#jstopbtn").addEventListener("click", stopJoint);

  setWorkingFromControls();
  runTranspose(false);
};
