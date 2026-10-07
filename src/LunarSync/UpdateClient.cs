using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using LunarSync.Core;

namespace LunarSync;
internal sealed class UpdateClient
{
    private readonly UpdateChannel channel;
    private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(40)};
    internal ReleaseManifest? Available {get;private set;}
    private byte[]? signedMetadata;
    internal bool Configured=>channel.Configured && !string.IsNullOrWhiteSpace(channel.ManifestUrl) && !string.IsNullOrWhiteSpace(channel.PublicKeyPem);
    internal string Status {get;private set;}="Publication à configurer";
    internal void ReportFailure(string message)=>Status="Vérification impossible : "+message;
    internal UpdateClient()
    {
        channel=JsonFiles.Read<UpdateChannel>(Path.Combine(AppContext.BaseDirectory,"update-channel.json"));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LunarSync/"+AppServices.Version);
        if(Configured)Status="Prêt à vérifier les mises à jour";
    }
    private static string SequenceFile=>Path.Combine(AppServices.DataRoot,"update-sequence.json");
    private static long HighestSequence()
    {try{return JsonFiles.Read<long>(SequenceFile);}catch{return 0;}}
    internal async Task CheckAsync()
    {
        if(!Configured){Status="Canal de publication non configuré";return;}
        Status="Recherche d’une mise à jour…";
        using var stream=new MemoryStream();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(40));
        await DownloadAsync(channel.ManifestUrl,stream,UpdateSecurity.MaxMetadataBytes,deadline.Token);
        byte[] bytes=stream.ToArray();
        var manifest=UpdateSecurity.Verify(bytes,channel,AppServices.Version,HighestSequence(),DateTimeOffset.UtcNow,true);
        JsonFiles.Write(SequenceFile,Math.Max(HighestSequence(),manifest.Sequence));
        if(Version.Parse(manifest.Version)==AppServices.Version){Available=null;Status="LunarSync est à jour";return;}
        Available=manifest;signedMetadata=bytes;Status="Il y a une mise à jour : "+manifest.Version;
    }
    private async Task DownloadAsync(string url,Stream destination,long max,CancellationToken token)
    {
        Uri uri=UpdateSecurity.TrustedUri(url,channel);
        for(int redirects=0;redirects<=4;redirects++)
        {
            using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode is >=300 and <400)
            {
                if(response.Headers.Location==null)throw new IOException("Redirection invalide.");
                uri=UpdateSecurity.TrustedUri(new Uri(uri,response.Headers.Location).AbsoluteUri,channel);continue;
            }
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>max)throw new IOException("Téléchargement trop volumineux.");
            using var source=await response.Content.ReadAsStreamAsync(token);
            var buffer=new byte[65536];long total=0;int length;
            while((length=await source.ReadAsync(buffer,token))>0)
            {total+=length;if(total>max)throw new IOException("Limite de téléchargement dépassée.");await destination.WriteAsync(buffer.AsMemory(0,length),token);}
            return;
        }
        throw new IOException("Trop de redirections.");
    }
    internal async Task InstallAsync()
    {
        if(Available==null||signedMetadata==null)throw new InvalidOperationException("Aucune mise à jour disponible.");
        string installApp=Path.Combine(AppServices.InstallRoot,"app");
        if(!Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\').Equals(installApp,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Installe LunarSync avec son installateur pour activer les mises à jour en un clic.");
        var verified=UpdateSecurity.Verify(signedMetadata,channel,AppServices.Version,HighestSequence(),DateTimeOffset.UtcNow);
        string stage=Path.Combine(AppServices.DataRoot,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        string package=Path.Combine(stage,"package.zip");
        using(var file=new FileStream(package,FileMode.CreateNew,FileAccess.Write))
        using(var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            await DownloadAsync(verified.Url,file,verified.Length,deadline.Token);
        UpdateSecurity.VerifyPackage(package,verified);
        await File.WriteAllBytesAsync(Path.Combine(stage,"release.json"),signedMetadata);
        string helper=Path.Combine(stage,"helper");Directory.CreateDirectory(helper);
        foreach(string source in Directory.EnumerateFiles(AppContext.BaseDirectory,"*",SearchOption.AllDirectories))
        {
            string target=Path.Combine(helper,Path.GetRelativePath(AppContext.BaseDirectory,source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);
        }
        var start=new ProcessStartInfo(Path.Combine(helper,"LunarSync.exe")){UseShellExecute=false};
        start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(Environment.ProcessId.ToString());start.ArgumentList.Add(stage);
        _=Process.Start(start)??throw new IOException("Impossible de démarrer l’assistant de mise à jour.");
    }
    internal static async Task ApplyStagedAsync(string[] args)
    {
        if(args.Length!=3||!int.TryParse(args[1],out int pid))throw new InvalidDataException("Arguments de mise à jour invalides.");
        string updates=Path.GetFullPath(Path.Combine(AppServices.DataRoot,"updates"))+Path.DirectorySeparatorChar;
        string stage=Path.GetFullPath(args[2]);
        if(!stage.StartsWith(updates,StringComparison.OrdinalIgnoreCase)||Path.GetDirectoryName(stage)+Path.DirectorySeparatorChar!=updates||!Guid.TryParseExact(Path.GetFileName(stage),"N",out _))
            throw new InvalidDataException("Dossier de préparation invalide.");
        var channel=JsonFiles.Read<UpdateChannel>(Path.Combine(AppContext.BaseDirectory,"update-channel.json"));
        var manifest=UpdateSecurity.Verify(await File.ReadAllBytesAsync(Path.Combine(stage,"release.json")),channel,AppServices.Version,HighestSequence(),DateTimeOffset.UtcNow);
        string package=Path.Combine(stage,"package.zip");UpdateSecurity.VerifyPackage(package,manifest);
        string root=AppServices.InstallRoot;
        if(!File.Exists(Path.Combine(root,"lunarsync.install")))throw new InvalidDataException("Installation introuvable.");
        string next=Path.Combine(root,"next-"+Guid.NewGuid().ToString("N"));
        UpdateSecurity.ExtractPackage(package,next);
        try
        {
            using var process=Process.GetProcessById(pid);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await process.WaitForExitAsync(deadline.Token);
        }catch(ArgumentException){ }
        string app=Path.Combine(root,"app"),backup=Path.Combine(root,"backup-"+Guid.NewGuid().ToString("N"));
        Directory.Move(app,backup);
        try{Directory.Move(next,app);Process.Start(new ProcessStartInfo(Path.Combine(app,"LunarSync.exe")){UseShellExecute=true});}
        catch
        {
            if(Directory.Exists(app))Directory.Move(app,next);
            Directory.Move(backup,app);
            Process.Start(new ProcessStartInfo(Path.Combine(app,"LunarSync.exe")){UseShellExecute=true});throw;
        }
    }
}
