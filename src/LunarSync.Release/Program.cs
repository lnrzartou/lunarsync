using System.Security.Cryptography;
using System.Text.Json;
using LunarSync.Core;
using LunarSync.Release;

try
{
    if(args.Length<1)throw new ArgumentException("Commandes : keygen-windows <dossier privé> | sign-windows <clé.dpapi> <paquet.zip> <version> <séquence> <URL HTTPS> <release.json> [notes.txt] | verify <canal.json> <release.json> <version actuelle> [paquet.zip] | export-windows <clé.dpapi> <sauvegarde.pem> | keygen / sign pour les clés PEM chiffrées.");
    if(args[0]=="keygen-windows")
    {
        if(args.Length!=2)throw new ArgumentException("Indique un dossier privé hors du projet.");
        WindowsSigningKey.Create(Path.GetFullPath(args[1]));Console.WriteLine("Clé de signature créée et protégée par ton compte Windows. Seule la clé publique peut être publiée.");
    }
    else if(args[0]=="export-windows")
    {
        if(args.Length!=3||File.Exists(args[2]))throw new ArgumentException("Indique la clé Windows et un nouveau fichier PEM privé de sauvegarde.");
        string password=ReadPassword("Phrase secrète de sauvegarde (16 caractères minimum) : ");
        if(password.Length<16||ReadPassword("Confirme la phrase secrète : ")!=password)throw new ArgumentException("Phrase trop courte ou différente.");
        using var key=WindowsSigningKey.Load(args[1]);
        using var file=new StreamWriter(new FileStream(args[2],FileMode.CreateNew,FileAccess.Write,FileShare.None));
        file.Write(key.ExportEncryptedPkcs8PrivateKeyPem(password,new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,600000)));
        Console.WriteLine("Sauvegarde privée chiffrée créée. Ne la publie pas.");
    }
    else if(args[0] is "verify" or "verify-history")
    {
        if(args.Length is <4 or >5)throw new ArgumentException("verify demande canal, manifeste, version actuelle et ZIP facultatif.");
        var channel=JsonFiles.Read<UpdateChannel>(args[1]);
        byte[] bytes=File.ReadAllBytes(args[2]);DateTimeOffset validationTime=DateTimeOffset.UtcNow;
        if(args[0]=="verify-history")
        {
            if(bytes.Length>UpdateSecurity.MaxMetadataBytes)throw new InvalidDataException("Manifeste trop volumineux.");
            var envelope=JsonSerializer.Deserialize<SignedEnvelope>(bytes,JsonFiles.Options)??throw new InvalidDataException("Manifeste absent.");
            var historical=JsonSerializer.Deserialize<ReleaseManifest>(Convert.FromBase64String(envelope.Payload),JsonFiles.Options)??throw new InvalidDataException("Métadonnées absentes.");
            validationTime=historical.PublishedUtc;
        }
        var manifest=UpdateSecurity.Verify(bytes,channel,Version.Parse(args[3]),0,validationTime,true);
        if(args.Length==5)UpdateSecurity.VerifyPackage(args[4],manifest);
        Console.WriteLine($"Signature valide · LunarSync {manifest.Version} · séquence {manifest.Sequence}"+(args[0]=="verify-history"?" · contrôle historique, sans validation de fraîcheur actuelle":"")+(args.Length==5?" · SHA-256 et taille vérifiés":""));
    }
    else if(args[0]=="keygen")
    {
        if(args.Length!=2)throw new ArgumentException("keygen demande un dossier privé hors du projet.");
        string folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        string keyPath=Path.Combine(folder,"lunarsync-private.pem"),publicPath=Path.Combine(folder,"lunarsync-public.pem");
        if(File.Exists(keyPath)||File.Exists(publicPath))throw new IOException("Une clé existe déjà : aucun écrasement autorisé.");
        string password=ReadPassword("Phrase secrète (16 caractères minimum) : ");if(password.Length<16)throw new ArgumentException("Phrase secrète trop courte.");
        if(ReadPassword("Confirme la phrase secrète : ")!=password)throw new ArgumentException("Les phrases ne correspondent pas.");
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(keyPath,key.ExportEncryptedPkcs8PrivateKeyPem(password,new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,600000)));
        File.WriteAllText(publicPath,key.ExportSubjectPublicKeyInfoPem());Console.WriteLine("Clé privée chiffrée et clé publique créées. Ne distribue jamais la clé privée.");
    }
    else if(args[0] is "sign" or "sign-windows")
    {
        if(args.Length is <7 or >8)throw new ArgumentException("sign demande clé, paquet, version, séquence, URL, sortie et notes facultatives.");
        using var key=args[0]=="sign-windows"?WindowsSigningKey.Load(args[1]):ReadPemKey(args[1]);
        if(key.KeySize!=256)throw new ArgumentException("Clé P-256 requise.");
        if(!Version.TryParse(args[3],out var version)||!long.TryParse(args[4],out var sequence)||sequence<1)throw new ArgumentException("Version ou séquence invalide.");
        if(!Uri.TryCreate(args[5],UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new ArgumentException("Une URL HTTPS est obligatoire.");
        var file=new FileInfo(args[2]);if(file.Length>UpdateSecurity.MaxPackageBytes)throw new ArgumentException("Paquet trop volumineux.");
        using var package=File.OpenRead(file.FullName);string hash=Convert.ToHexString(SHA256.HashData(package));var now=DateTimeOffset.UtcNow;
        string notes=args.Length==8?File.ReadAllText(args[7]):"Nouvelle version de LunarSync.";
        var manifest=new ReleaseManifest(1,"LunarSync","stable",version.ToString(),sequence,now,now.AddDays(45),uri.AbsoluteUri,file.Length,hash,notes);
        byte[] payload=JsonSerializer.SerializeToUtf8Bytes(manifest,JsonFiles.Options);byte[] signature=key.SignData(payload,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        JsonFiles.Write(args[6],new SignedEnvelope(Convert.ToBase64String(payload),Convert.ToBase64String(signature)));
        Console.WriteLine("Publication signée : "+Path.GetFullPath(args[6]));
    }
    else throw new ArgumentException("Commande inconnue.");
    return 0;
}
catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);var buffer=new List<char>();
    while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(buffer.Count>0)buffer.RemoveAt(buffer.Count-1);}else if(!char.IsControl(key.KeyChar))buffer.Add(key.KeyChar);}
    Console.WriteLine();return new string(buffer.ToArray());
}

static ECDsa ReadPemKey(string file)
{
    string password=ReadPassword("Phrase secrète de la clé : ");var key=ECDsa.Create();
    try{key.ImportFromEncryptedPem(File.ReadAllText(file),password);return key;}catch{key.Dispose();throw;}
}
