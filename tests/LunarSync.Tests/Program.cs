using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LunarSync.Core;
using LunarSync.Release;

int passed=0,failed=0;
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);passed++;}catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}}
void Assert(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
void Reject(Action action){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,"Expected rejection");}
MacroDefinition Def(string id="hold-f",int key=70,int toggle=38,MacroKind kind=MacroKind.Hold)=>new(id,id,"Test",kind,key,toggle,35);
MacroConfig Conf(MacroDefinition d){var c=MacroConfig.Default(d);c.Configured=true;return c;}

Check("hold forwards base and releases left",()=>{double now=0;var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);Assert(e.Input(38,true));e.Input(38,false);Assert(!e.Input(70,true));now=3;e.Tick();Assert(o.Events.SequenceEqual(new[]{(1,true)}));now=500;e.Input(70,false);Assert(o.Events.Last()==(1,false));});
Check("hold autorepeat emits one down",()=>{double now=0;var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(70,true);now=4;e.Tick();for(int i=0;i<30;i++)e.Input(70,true);Assert(o.Events.Count==1);e.Input(70,false);Assert(o.Events.Count==2);});
Check("overlapping holds release on last key",()=>{double now=0;var o=new FakeOutput();var a=Def();var b=Def("hold-x",88,40);using var e=new MacroEngine(o,()=>now);e.Configure([a,b],[Conf(a),Conf(b)]);e.SetReady(true);e.Toggle(a.Id);e.Toggle(b.Id);e.Input(70,true);now=4;e.Tick();e.Input(88,true);now=8;e.Tick();e.Input(70,false);Assert(o.Events.Count==1);e.Input(88,false);Assert(o.Events.Count==2);});
Check("independent toggles keep other active hold",()=>{double now=0;var o=new FakeOutput();var a=Def();var b=Def("hold-x",88,40);using var e=new MacroEngine(o,()=>now);e.Configure([a,b],[Conf(a),Conf(b)]);e.SetReady(true);e.Toggle(a.Id);e.Toggle(b.Id);e.Input(70,true);e.Input(88,true);now=4;e.Tick();e.Toggle(a.Id);Assert(o.Events.Count==1);e.Toggle(b.Id);Assert(o.Events.Last()==(1,false));});
Check("disabled held key never retains another macro click",()=>{double now=0;var o=new FakeOutput();var a=Def();var b=Def("hold-x",88,40);using var e=new MacroEngine(o,()=>now);e.Configure([a,b],[Conf(a),Conf(b)]);e.SetReady(true);e.Toggle(a.Id);e.Input(88,true);e.Input(70,true);now=4;e.Tick();e.Input(70,false);Assert(o.Events.Last()==(1,false));});
Check("off releases while trigger is still held",()=>{double now=0;var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(70,true);now=3;e.Tick();e.Toggle(d.Id);Assert(o.Events.Last()==(1,false));now=100;e.Input(70,true);e.Tick();Assert(o.Events.Count==2);});
Check("stop preserves physical left hold",()=>{double now=0;var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(1,true);e.Input(70,true);now=3;e.Tick();e.StopAll();Assert(!o.Events.Contains((1,false)));});
Check("physical left release reasserts active hold",()=>{double now=0;var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(70,true);now=3;e.Tick();e.Input(1,true);e.Input(1,false);now=6;e.Tick();Assert(o.Events.Count==2&&o.Events.Last()==(1,true));});
Check("no keyboard selection prevents activation",()=>{var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>0);e.Configure([d],[Conf(d)]);Assert(!e.Input(38,true));e.Toggle(d.Id);Assert(!e.Snapshot()[0].Enabled);});
Check("unconfigured preset does not intercept toggle",()=>{var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>0);e.Configure([d],[MacroConfig.Default(d)]);e.SetReady(true);Assert(!e.Input(38,true));Assert(!e.Snapshot()[0].Enabled);});
Check("shared toggle activates and disables group once",()=>{var a=Def();var b=Def("hold-c",67,38);using var e=new MacroEngine(new FakeOutput(),()=>0);e.Configure([a,b],[Conf(a),Conf(b)]);e.SetReady(true);e.Input(38,true);e.Input(38,true);Assert(e.Snapshot().All(s=>s.Enabled));e.Input(38,false);e.Input(38,true);Assert(e.Snapshot().All(s=>!s.Enabled));});
Check("repeat key then click preserves event order",()=>{double now=0;var o=new FakeOutput();var d=Def(kind:MacroKind.KeyAndClick);using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Tick();now=3;e.Tick();now=6;e.Tick();now=9;e.Tick();Assert(o.Events.SequenceEqual(new[]{(70,true),(70,false),(1,true),(1,false)}));});
Check("repeat stop releases active key and no later events",()=>{double now=0;var o=new FakeOutput();var d=Def(kind:MacroKind.Key);using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Tick();e.StopAll();now=500;e.Tick();Assert(o.Events.SequenceEqual(new[]{(70,true),(70,false)}));});
Check("disconnect cancels all held inputs",()=>{double now=0;var o=new FakeOutput();var d=Def();using var e=new MacroEngine(o,()=>now);e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(70,true);now=3;e.Tick();e.SetReady(false);Assert(o.Events.Last()==(1,false)&&!e.Snapshot()[0].Enabled);});
Check("irregular periods stay bounded and average 35 cps",()=>{var c=Conf(Def(kind:MacroKind.Key));c.Irregular=true;var random=new Random(19);var periods=Enumerable.Range(0,100000).Select(_=>MacroEngine.NextPeriod(c,MacroKind.Key,random)).ToArray();Assert(periods.Min()>=1000/35d*.85&&periods.Max()<=1000/35d*1.15);Assert(Math.Abs(1000/periods.Average()-35)<.05);Assert(periods.Distinct().Count()>100);});
Check("invalid cadence and collision are rejected",()=>{var d=Def();var c=Conf(d);c.Cps=double.NaN;Reject(()=>c.Validate(d));c=Conf(d);c.ToggleKey=c.BaseKey;Reject(()=>c.Validate(d));});

Check("ten fixed profiles migrate legacy settings independently",()=>{
    var d=Def();var old=Conf(d);old.BaseKey=88;
    var s=new UserSettings{SchemaVersion=1,Macros=[old]};s.Normalize([d]);
    Assert(s.SchemaVersion==2&&s.Profiles.Count==10&&s.Macros==null);
    Assert(s.Profiles.Select(p=>p.Slot).SequenceEqual(Enumerable.Range(1,10)));
    Assert(s.Profiles[0].Macros[0].Configured&&s.Profiles[0].Macros[0].BaseKey==88);
    Assert(s.Profiles.Skip(1).All(p=>!p.Macros[0].Configured));
    s.Profiles[1].Macros[0].BaseKey=67;Assert(s.Profiles[2].Macros[0].BaseKey==70&&old.BaseKey==88);
});
Check("profile normalization preserves v2 and rejects extra slots",()=>{
    var d=Def();var s=new UserSettings{SelectedProfile=99,Profiles=[new(){Slot=2,Macros=[Conf(d)]},new(){Slot=15,Macros=[Conf(d)]}]};
    s.Normalize([d]);Assert(s.SelectedProfile==10&&s.Profiles.Count==10&&s.Profiles[1].Macros[0].Configured);
    s.Normalize([d]);Assert(s.Profiles.Count==10&&s.Profiles[1].Macros[0].Configured);
});
Check("switching to empty profile releases and frees bindings",()=>{
    double now=0;var d=Def();var o=new FakeOutput();using var e=new MacroEngine(o,()=>now);
    e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(70,true);now=3;e.Tick();
    e.Configure([d],[MacroConfig.Default(d)]);Assert(o.Events.Last()==(1,false));Assert(!e.Input(38,true));
    now=100;e.Tick();Assert(!e.Snapshot()[0].Enabled&&o.Events.Count==2);
});
Check("statistics count hold base plus actual click once",()=>{
    double now=0;var d=Def();var tracker=new StatisticsTracker();using var e=new MacroEngine(new FakeOutput(),()=>now);
    e.ActionKeyPressed+=tracker.KeyPressed;e.MacroUsed+=tracker.MacroUsed;e.Configure([d],[Conf(d)]);e.SetReady(true);
    e.Input(38,true);e.Input(38,false);e.Input(70,true);now=3;e.Tick();
    for(int i=0;i<20;i++)e.Input(70,true);e.Input(70,false);e.Input(65,true);e.Input(65,false);
    var s=tracker.Snapshot();Assert(s.Keys.Count==2&&s.Keys[70]==1&&s.Keys[1]==1&&s.Macros[d.Id]==1);
});
Check("disabled macro and unrelated typing do not create counters",()=>{
    var d=Def();var tracker=new StatisticsTracker();using var e=new MacroEngine(new FakeOutput(),()=>0);
    e.ActionKeyPressed+=tracker.KeyPressed;e.Configure([d],[Conf(d)]);e.SetReady(true);
    e.Input(70,true);e.Input(70,false);e.Input(65,true);e.Input(65,false);e.Tick();Assert(tracker.Snapshot().Keys.Count==0);
});
Check("repeat sequence records each generated down only",()=>{
    double now=0;var d=Def(kind:MacroKind.KeyAndClick);var tracker=new StatisticsTracker();using var e=new MacroEngine(new FakeOutput(),()=>now);
    e.ActionKeyPressed+=tracker.KeyPressed;e.MacroUsed+=tracker.MacroUsed;e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);
    e.Tick();now=3;e.Tick();now=6;e.Tick();now=9;e.Tick();
    var s=tracker.Snapshot();Assert(s.Keys[70]==1&&s.Keys[1]==1&&s.Macros[d.Id]==1);
});
Check("overlapping hold statistics reflect a shared click",()=>{
    double now=0;var a=Def();var b=Def("x",88,40);var tracker=new StatisticsTracker();using var e=new MacroEngine(new FakeOutput(),()=>now);
    e.ActionKeyPressed+=tracker.KeyPressed;e.Configure([a,b],[Conf(a),Conf(b)]);e.SetReady(true);e.Toggle(a.Id);e.Toggle(b.Id);
    e.Input(70,true);e.Input(88,true);now=3;e.Tick();var s=tracker.Snapshot();Assert(s.Keys[70]==1&&s.Keys[88]==1&&s.Keys[1]==1);
});
Check("short hold counts base without a click that never happened",()=>{
    double now=0;var d=Def();var tracker=new StatisticsTracker();using var e=new MacroEngine(new FakeOutput(),()=>now);
    e.ActionKeyPressed+=tracker.KeyPressed;e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);
    e.Input(70,true);now=1;e.Input(70,false);now=4;e.Tick();var s=tracker.Snapshot();Assert(s.Keys.Count==1&&s.Keys[70]==1);
});
Check("statistics separate profiles and survive serialization",()=>{
    var t=new StatisticsTracker();t.KeyPressed(70);t.KeyPressed(1);t.MacroUsed("hold-f");t.SelectProfile(2);t.KeyPressed(88);
    var bytes=JsonSerializer.SerializeToUtf8Bytes(t.Export(),JsonFiles.Options);
    var restored=new StatisticsTracker(JsonSerializer.Deserialize<UsageStatistics>(bytes,JsonFiles.Options));
    Assert(restored.Snapshot(1).Keys.Count==2&&restored.Snapshot(2).Keys[88]==1&&restored.Snapshot().Keys.Count==3);
    var snapshot=restored.Export();snapshot.Profiles[1].Keys[70]=100;Assert(restored.Snapshot(1).Keys[70]==1);
});
Check("failed output is not counted as a click",()=>{
    double now=0;var d=Def();var tracker=new StatisticsTracker();using var e=new MacroEngine(new FailedOutput(),()=>now);
    e.ActionKeyPressed+=tracker.KeyPressed;e.Configure([d],[Conf(d)]);e.SetReady(true);e.Toggle(d.Id);e.Input(70,true);now=3;
    Reject(e.Tick);Assert(!tracker.Snapshot().Keys.ContainsKey(1));
});

using var signing=ECDsa.Create(ECCurve.NamedCurves.nistP256);
var channel=new UpdateChannel{Configured=true,PublicKeyPem=signing.ExportSubjectPublicKeyInfoPem(),AllowedHosts=["updates.example.com"],Channel="stable"};
var nowUtc=DateTimeOffset.UtcNow;
var release=new ReleaseManifest(1,"LunarSync","stable","1.1.0",2,nowUtc,nowUtc.AddDays(30),"https://updates.example.com/v1.1.zip",10,new string('A',64),"Test");
byte[] Sign(ReleaseManifest m){var b=JsonSerializer.SerializeToUtf8Bytes(m,JsonFiles.Options);return JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope(Convert.ToBase64String(b),Convert.ToBase64String(signing.SignData(b,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))),JsonFiles.Options);}
Check("valid signed publication accepted",()=>Assert(UpdateSecurity.Verify(Sign(release),channel,new(1,0,0),1,nowUtc).Version=="1.1.0"));
Check("forged payload rejected",()=>{var data=JsonSerializer.Deserialize<SignedEnvelope>(Sign(release),JsonFiles.Options)!;var forged=data with{Payload=Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"))};Reject(()=>UpdateSecurity.Verify(JsonSerializer.SerializeToUtf8Bytes(forged,JsonFiles.Options),channel,new(1,0,0),1,nowUtc));});
Check("wrong signing key rejected",()=>{using var wrong=ECDsa.Create(ECCurve.NamedCurves.nistP256);var other=new UpdateChannel{Configured=true,PublicKeyPem=wrong.ExportSubjectPublicKeyInfoPem(),AllowedHosts=channel.AllowedHosts};Reject(()=>UpdateSecurity.Verify(Sign(release),other,new(1,0,0),1,nowUtc));});
Check("expired publication rejected",()=>Reject(()=>UpdateSecurity.Verify(Sign(release with{PublishedUtc=nowUtc.AddDays(-3),ExpiresUtc=nowUtc.AddDays(-1)}),channel,new(1,0,0),1,nowUtc)));
Check("version rollback rejected",()=>Reject(()=>UpdateSecurity.Verify(Sign(release with{Version="0.9.0"}),channel,new(1,0,0),1,nowUtc)));
Check("sequence rollback rejected",()=>Reject(()=>UpdateSecurity.Verify(Sign(release),channel,new(1,0,0),5,nowUtc)));
Check("foreign product and channel rejected",()=>{Reject(()=>UpdateSecurity.Verify(Sign(release with{Product="Other"}),channel,new(1,0,0),1,nowUtc));Reject(()=>UpdateSecurity.Verify(Sign(release with{Channel="beta"}),channel,new(1,0,0),1,nowUtc));});
Check("HTTP and unapproved host rejected",()=>{Reject(()=>UpdateSecurity.TrustedUri("http://updates.example.com/file",channel));Reject(()=>UpdateSecurity.TrustedUri("https://evil.example.com/file",channel));Reject(()=>UpdateSecurity.TrustedUri("https://user@updates.example.com/file",channel));});
Check("unconfigured channel rejects all metadata",()=>Reject(()=>UpdateSecurity.Verify(Sign(release),new(),new(1,0,0),0,nowUtc)));
string scratch=Path.Combine(Path.GetTempPath(),"LunarSync-Tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
Check("Windows signing key stays encrypted and signs with public key",()=>
{
    string folder=Path.Combine(scratch,"publisher");WindowsSigningKey.Create(folder);
    string privateFile=Path.Combine(folder,"lunarsync-private.dpapi");using var key=WindowsSigningKey.Load(privateFile);
    using var publicKey=ECDsa.Create();publicKey.ImportFromPem(File.ReadAllText(Path.Combine(folder,"lunarsync-public.pem")));
    byte[] message=Encoding.UTF8.GetBytes("test publication"),signature=key.SignData(message,HashAlgorithmName.SHA256);
    Assert(publicKey.VerifyData(message,signature,HashAlgorithmName.SHA256));
    byte[] encrypted=File.ReadAllBytes(privateFile),clear=key.ExportPkcs8PrivateKey();Assert(!encrypted.SequenceEqual(clear));CryptographicOperations.ZeroMemory(clear);
    Reject(()=>WindowsSigningKey.Create(folder));
    encrypted[encrypted.Length/2]^=1;string bad=Path.Combine(folder,"tampered.dpapi");File.WriteAllBytes(bad,encrypted);Reject(()=>WindowsSigningKey.Load(bad));
});
Check("package hash and length verified",()=>{string file=Path.Combine(scratch,"file");File.WriteAllText(file,"hello");var m=release with{Length=5,Sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello")))};UpdateSecurity.VerifyPackage(file,m);File.WriteAllText(file,"hallo");Reject(()=>UpdateSecurity.VerifyPackage(file,m));});
Check("zip path traversal rejected",()=>{string zip=Path.Combine(scratch,"bad.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("../outside.txt").Open());w.Write("bad");}Reject(()=>UpdateSecurity.ExtractPackage(zip,Path.Combine(scratch,"bad")));Assert(!File.Exists(Path.Combine(scratch,"outside.txt")));});
Check("valid package extracted",()=>{string zip=Path.Combine(scratch,"good.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("LunarSync.exe").Open());w.Write("test");}string destination=Path.Combine(scratch,"good");UpdateSecurity.ExtractPackage(zip,destination);Assert(File.Exists(Path.Combine(destination,"LunarSync.exe")));});
Directory.Delete(scratch,true);
Console.WriteLine($"\n{passed} passed, {failed} failed");return failed==0?0:1;

sealed class FakeOutput:IInputOutput{public List<(int,bool)> Events=[];public void SetKey(int key,bool down)=>Events.Add((key,down));}
sealed class FailedOutput:IInputOutput{public void SetKey(int key,bool down){if(down)throw new IOException("Test refused output");}}
