import fs from 'node:fs/promises';
import path from 'node:path';
const [folder,port,bid,aid]=process.argv.slice(2);
const base='http://127.0.0.1:'+port;
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
async function until(test,label) {
  for(let i=0;i<100;i++){const result=await test();if(result)return result;await sleep(150);}
  throw new Error('Timed out: '+label);
}
const cdp=await until(async()=>{try{return (await fs.readFile(path.join(folder,'DevToolsActivePort'),'utf8')).split(/\r?\n/)[0]}catch{return null}},'Edge startup');
async function page(){
 const meta=await(await fetch('http://127.0.0.1:'+cdp+'/json/new?'+encodeURIComponent(base),{method:'PUT'})).json();
 const sock=new WebSocket(meta.webSocketDebuggerUrl);
 await new Promise((ok,fail)=>{sock.onopen=ok;sock.onerror=fail});
 let id=0;const pending=new Map();sock.onmessage=e=>{const m=JSON.parse(e.data);if(pending.has(m.id)){pending.get(m.id)(m);pending.delete(m.id)}};
 async function evalJs(expression){const n=++id;const p=new Promise(ok=>pending.set(n,ok));sock.send(JSON.stringify({id:n,method:'Runtime.evaluate',params:{expression,awaitPromise:true,returnByValue:true}}));const r=await p;if(r.error||r.result.exceptionDetails)throw Error('Browser evaluation failed');return r.result.result.value;}
 await until(()=>evalJs('typeof document.querySelector("#questionForm")?.onsubmit === "function"'),'Atelier controls');
 return evalJs;
}
const first=await page(),second=await page();
async function ask(tab,theme){
 await tab("document.querySelector('#composeForm').elements.theme.value="+JSON.stringify(theme)+"; document.querySelector('#questionForm').elements.question.value='Review this theme'; document.querySelector('#questionForm').requestSubmit(); true");
 return until(()=>tab("JSON.parse(sessionStorage.getItem('theoria.atelier.'+window.atelierServerId)??'{}').id"),'consultation receipt');
}
const id=await ask(first,'first tab');const other=await ask(second,'second tab');if(id===other)throw Error('shared consultation ID');
await fs.writeFile(path.join(folder,'question-id'),id);
await until(()=>first("document.querySelector('#studioPreview')?.textContent.includes('fixture-reply')"),'Codex reply');
if(await second("document.querySelector('#studioPreview').textContent.includes('fixture-reply') || document.querySelector('#composeForm').elements.theme.value!=='second tab'"))throw Error('Second tab changed');
async function select(id){const r=await fetch(base+'/atelier/servers/'+id+'/select',{method:'POST'});if(!r.ok)throw Error('Select failed');}
await select(bid);const third=await page();await ask(third,'server B');
await select(aid);await first('location.reload();true');
await until(()=>first("document.querySelector('#composeForm')?.elements.theme.value==='first tab'"),'restore A draft');
console.log('PASS actual Atelier: two distinct consultations, Codex reply reached original tab, original draft restored after switching servers');
const version=await(await fetch('http://127.0.0.1:'+cdp+'/json/version')).json();
const browser=new WebSocket(version.webSocketDebuggerUrl);
await new Promise((ok,fail)=>{browser.onopen=ok;browser.onerror=fail});
await new Promise((ok,fail)=>{const timer=setTimeout(()=>fail(Error('Edge did not shut down')),10000);
  browser.onclose=()=>{clearTimeout(timer);ok()};browser.send(JSON.stringify({id:1,method:'Browser.close'}));});
process.exit(0);
