from pathlib import Path
import json,subprocess,tempfile,time,socket,os,sys,urllib.request,shutil,queue,threading
from contextlib import ExitStack, contextmanager
import argparse
parser=argparse.ArgumentParser(description="Check actual Atelier tabs and Codex against the AIKernel package.")
parser.add_argument("--music-root",type=Path,required=True)
parser.add_argument("--dotnet",default="dotnet")
parser.add_argument("--edge",type=Path,required=True)
parser.add_argument("--fixture",type=Path)
args=parser.parse_args()
stage=Path(__file__).resolve().parent
root=stage
fixture=args.fixture or stage/'bin/Release/net10.0/Music.Atelier.Compatibility.dll'
atelier=args.music_root/'src/TheoriaCast.Music.Atelier/bin/Release/net10.0/TheoriaCast.Music.Atelier.dll'
for file in (fixture,atelier,args.edge):
    if not file.is_file():parser.error("Missing prerequisite: "+str(file))
def await_file(p,n=300,process=None):
    for _ in range(n):
        if p.is_file(): return p
        if process is not None and process.poll() is not None:
            raise RuntimeError(f"Process exited with {process.returncode} before creating {p.name}")
        time.sleep(.1)
    raise RuntimeError('Timeout waiting for '+p.name)
@contextmanager
def private_test_directory():
    directory = tempfile.TemporaryDirectory(prefix='aikernel-atelier-')
    try:
        yield directory.name
    finally:
        # Edge can retain file handles briefly after acknowledging Browser.close.
        for attempt in range(20):
            try:
                directory.cleanup()
                break
            except PermissionError:
                if attempt == 19:
                    raise
                time.sleep(.5)

def stop_browser_helpers(profile):
    if os.name != "nt":
        return
    # Edge may relaunch a helper outside the original process tree. Match only our unique profile.
    command = """$id = $env:AIKERNEL_FIXTURE_PROFILE_ID
if ($id -notmatch '^aikernel-atelier-[a-z0-9_]+$') { exit 1 }
Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Contains($id) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }"""
    environment = dict(os.environ, AIKERNEL_FIXTURE_PROFILE_ID=profile.parent.name)
    subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", command],
                   env=environment, check=True, creationflags=subprocess.CREATE_NO_WINDOW)

with private_test_directory() as temp, ExitStack() as cleanup:
    temp=Path(temp)
    def start(args,quiet=True,**extra):
        output = subprocess.DEVNULL if quiet else None
        p=subprocess.Popen(args,stderr=output,stdout=output,**extra)
        def stop():
            if p.poll() is None:p.terminate()
            p.wait(timeout=15)
        cleanup.callback(stop)
        return p
    configs=[]
    for name in ['first','second']:
        data=temp/name
        server = start([args.dotnet,str(fixture),str(data),name])
        configs.append(json.loads(await_file(data/'connection.json',process=server).read_text()))
    a,b=configs
    with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
    gateway=start([args.dotnet,str(atelier),'--port',str(port),'--profile',str(temp/'profiles')])
    url='http://127.0.0.1:'+str(port)
    for _ in range(120):
        try:urllib.request.urlopen(url+'/atelier/servers',timeout=1).close();break
        except OSError:time.sleep(.1)
    for config in [b,a]:
        req=urllib.request.Request(url+'/atelier/servers',data=json.dumps(config).encode(),headers={'Content-Type':'application/json'})
        with urllib.request.urlopen(req) as r:assert config['token'] not in r.read().decode()
    profile=temp/'edge'
    cleanup.callback(stop_browser_helpers, profile)
    start([str(args.edge),'--headless=new','--disable-gpu','--disable-background-networking','--disable-sync','--no-first-run','--remote-debugging-port=0','--user-data-dir='+str(profile),'about:blank'],creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    node=start([shutil.which('node'),str(stage/'check-tabs.mjs'),str(profile),str(port),b['server_id'],a['server_id']],quiet=False)
    ident=await_file(profile/'question-id',450,process=node).read_text()
    # Test only the MCP connection and an ephemeral session. No model turn is started.
    env=os.environ.copy();env['AIKERNEL_TEST_MCP_TOKEN']=a['token']
    cli=subprocess.Popen([shutil.which('codex'),'app-server','-c','mcp_servers.aikernel_test.url='+json.dumps(a['mcp_url']),'-c','mcp_servers.aikernel_test.bearer_token_env_var="AIKERNEL_TEST_MCP_TOKEN"'],env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True,encoding='utf8')
    cleanup.callback(lambda:(cli.terminate(),cli.wait(timeout=10)))
    q=queue.Queue()
    def pump():
        for line in cli.stdout:
            try:q.put(json.loads(line))
            except ValueError:pass
    threading.Thread(target=pump,daemon=True).start()
    def send(msg):cli.stdin.write(json.dumps(msg)+'\n');cli.stdin.flush()
    def call(i,method,params):
        send(dict(id=i,method=method,params=params))
        while True:
            msg=q.get(timeout=30)
            if msg.get('id')==i:
                if 'error' in msg:raise RuntimeError('Codex call failed: '+method)
                return msg['result']
    call(1,'initialize',{'clientInfo':{'name':'aikernel_atelier_acceptance','version':'0.1.4'},'capabilities':{'experimentalApi':True}});send({'method':'initialized'})
    tid=call(2,'thread/start',{'ephemeral':True,'cwd':str(root),'approvalPolicy':'never'})['thread']['id']
    tool=lambda i,name,args:call(i,'mcpServer/tool/call',{'threadId':tid,'server':'aikernel_test','tool':name,'arguments':args})
    saved=tool(3,'get_studio_request',{'id':ident})
    assert 'first tab' in json.dumps(saved)
    answer=tool(4,'suggest_in_studio',{'id':ident,'reply':'fixture-reply','changes':{'theme':'reviewed fixture'}})
    assert 'fixture-reply' in json.dumps(answer)
    if node.wait(timeout=45):raise RuntimeError('Atelier browser assertions failed')
print('PASS packaged AIKernel + actual MusicStore, MusicTools, Atelier GUI and Codex; two servers, two tabs, suggestion and draft isolation.')
