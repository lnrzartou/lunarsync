using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LunarSync.Core;

public sealed class UpdateChannel
{
    public bool Configured {get;set;}
    public string ManifestUrl {get;set;}="";
    public string PublicKeyPem {get;set;}="";
    public string[] AllowedHosts {get;set;}=[];
    public string Channel {get;set;}="stable";
}
public sealed record SignedEnvelope(string Payload,string Signature);
public sealed record ReleaseManifest(int Schema,string Product,string Channel,string Version,long Sequence,
    DateTimeOffset PublishedUtc,DateTimeOffset ExpiresUtc,string Url,long Length,string Sha256,string Notes);

public static class UpdateSecurity
{
    public const long MaxPackageBytes=400L*1024*1024;
    public const int MaxMetadataBytes=128*1024;
    public static Uri TrustedUri(string text,UpdateChannel channel)
    {
        if(!Uri.TryCreate(text,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||uri.UserInfo.Length!=0||!uri.IsDefaultPort||
           uri.IsLoopback||Uri.CheckHostName(uri.Host)!=UriHostNameType.Dns||
           !channel.AllowedHosts.Any(h=>string.Equals(h,uri.IdnHost,StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Adresse de mise à jour non autorisée.");
        return uri;
    }
    public static ReleaseManifest Verify(byte[] envelope,UpdateChannel channel,Version current,long highestSequence,DateTimeOffset now,bool allowCurrent=false)
    {
        if(!channel.Configured||string.IsNullOrWhiteSpace(channel.PublicKeyPem)||envelope.Length>MaxMetadataBytes)
            throw new InvalidDataException("Canal de publication non configuré ou réponse trop volumineuse.");
        var e=JsonSerializer.Deserialize<SignedEnvelope>(envelope,JsonFiles.Options)??throw new InvalidDataException("Signature absente.");
        byte[] payload=Convert.FromBase64String(e.Payload),signature=Convert.FromBase64String(e.Signature);
        using var key=ECDsa.Create();key.ImportFromPem(channel.PublicKeyPem);
        if(key.KeySize!=256||!key.VerifyData(payload,signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new CryptographicException("La signature de Lunar n’est pas valide.");
        var m=JsonSerializer.Deserialize<ReleaseManifest>(payload,JsonFiles.Options)??throw new InvalidDataException("Publication vide.");
        if(m.Schema!=1||m.Product!="LunarSync"||m.Channel!=channel.Channel)throw new InvalidDataException("Publication incompatible.");
        if(!Version.TryParse(m.Version,out var version)||version<current||(!allowCurrent&&version==current)||m.Sequence<highestSequence||m.Sequence<1)
            throw new InvalidDataException("Publication ancienne ou déjà installée.");
        if(m.PublishedUtc>now.AddMinutes(15)||m.ExpiresUtc<=now||m.ExpiresUtc<=m.PublishedUtc||m.ExpiresUtc-m.PublishedUtc>TimeSpan.FromDays(90))
            throw new InvalidDataException("Publication expirée ou date invalide.");
        if(m.Length<1||m.Length>MaxPackageBytes||m.Sha256.Length!=64||!m.Sha256.All(Uri.IsHexDigit)||m.Notes.Length>20000)
            throw new InvalidDataException("Métadonnées invalides.");
        TrustedUri(m.Url,channel);return m;
    }
    public static void VerifyPackage(string file,ReleaseManifest manifest)
    {
        if(new FileInfo(file).Length!=manifest.Length)throw new InvalidDataException("Téléchargement incomplet.");
        using var stream=File.OpenRead(file);
        var hash=Convert.ToHexString(SHA256.HashData(stream));
        if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash),Encoding.ASCII.GetBytes(manifest.Sha256.ToUpperInvariant())))
            throw new CryptographicException("Le fichier téléchargé a été altéré.");
    }
    public static void ExtractPackage(string archive,string destination)
    {
        string root=Path.GetFullPath(destination)+Path.DirectorySeparatorChar;
        if(Directory.Exists(destination)&&Directory.EnumerateFileSystemEntries(destination).Any())throw new IOException("Le dossier de destination doit être vide.");
        Directory.CreateDirectory(destination);
        using var zip=ZipFile.OpenRead(archive);
        long expanded=0;
        if(zip.Entries.Count>5000)throw new InvalidDataException("Archive trop volumineuse.");
        foreach(var entry in zip.Entries)
        {
            string relative=entry.FullName.Replace('/',Path.DirectorySeparatorChar);
            if(Path.IsPathRooted(relative)||relative.Contains(':')||relative.Split(Path.DirectorySeparatorChar).Any(p=>p==".."||p.EndsWith(' ')||p.EndsWith('.')))
                throw new InvalidDataException("Chemin d’archive interdit.");
            string target=Path.GetFullPath(Path.Combine(destination,relative));
            if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)
                throw new InvalidDataException("Chemin d’archive hors dossier ou lien symbolique.");
            expanded+=entry.Length;
            if(expanded>1024L*1024*1024)throw new InvalidDataException("Archive décompressée trop volumineuse.");
            if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(target);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source=entry.Open();using var dest=new FileStream(target,FileMode.CreateNew,FileAccess.Write);
            source.CopyTo(dest);
        }
        if(!File.Exists(Path.Combine(destination,"LunarSync.exe")))throw new InvalidDataException("L’application est absente du paquet.");
    }
}
