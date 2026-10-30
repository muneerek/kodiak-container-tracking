using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;

namespace Kodiak.Setup;
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        string? root=null;
        try {
            if(!Environment.Is64BitOperatingSystem) throw new InvalidOperationException("Kodiak Setup requires 64-bit Windows.");
            var parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"KodiakSetup","Packages");
            Directory.CreateDirectory(parent);
            root=Path.Combine(parent,Guid.NewGuid().ToString("N"));
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            foreach(var sid in new[] {WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})
                acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            FileSystemAclExtensions.CreateDirectory(acl,root);
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("Kodiak.Payload.zip")
                ?? throw new InvalidOperationException("The installer payload is missing."))
            using(var archive=new ZipArchive(resource,ZipArchiveMode.Read)) {
                archive.ExtractToDirectory(root); // ZipArchive rejects paths outside the extraction directory.
            }
            using(var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"manifest.json")))) {
                foreach(var item in manifest.RootElement.GetProperty("files").EnumerateArray()) {
                    var path=Path.GetFullPath(Path.Combine(root,item.GetProperty("path").GetString()!));
                    if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The package contains an invalid file path.");
                    using var file=File.OpenRead(path);
                    if(!Convert.ToHexString(SHA256.HashData(file)).Equals(item.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The setup package failed its integrity check. Obtain a new installer.");
                }
            }
            var script=Path.Combine(root,"scripts","Setup-Wizard.ps1");
            var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32","WindowsPowerShell","v1.0","powershell.exe")) {UseShellExecute=false,CreateNoWindow=true};
            foreach(var arg in new[] {"-NoProfile","-ExecutionPolicy","Bypass","-STA","-File",script,"-PackageRoot",root}) info.ArgumentList.Add(arg);
            using var process=Process.Start(info) ?? throw new InvalidOperationException("Could not start the configuration wizard.");
            process.WaitForExit();
            if(process.ExitCode!=0) MessageBox.Show("The configuration wizard could not complete. Check the installation's setup.log, or run scripts/Setup-Wizard.ps1 from the deployment package for diagnostics.","Kodiak Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);
            return process.ExitCode;
        } catch(Exception e) {
            MessageBox.Show(e.Message,"Kodiak Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
        } finally {
            if(root!=null && Directory.Exists(root)) {
                // Only remove the exact GUID staging folder created by this process.
                try { Directory.Delete(root,true); } catch { /* Retain locked staging files for diagnosis. */ }
            }
        }
    }
}
