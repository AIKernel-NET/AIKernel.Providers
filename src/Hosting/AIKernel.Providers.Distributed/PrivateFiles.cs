using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace AIKernel.Providers.Distributed;

internal static class PrivateFiles
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal static void CheckPath(string path)
    {
        for (FileSystemInfo? part = new FileInfo(Path.GetFullPath(path)); part != null;
             part = part is FileInfo file ? file.Directory : ((DirectoryInfo)part).Parent)
            if (part.LinkTarget != null || part.Exists && part.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("A private path cannot contain links.");
    }

    internal static void Write<T>(string path, T value, bool overwrite = true)
    {
        path = Path.GetFullPath(path);
        CheckPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = Create(temp))
            {
                JsonSerializer.Serialize(file, value, Json);
                file.Flush(true);
            }
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite);
                    break;
                }
                catch (Exception error) when (OperatingSystem.IsWindows() && overwrite && attempt < 6 &&
                    (error is IOException or UnauthorizedAccessException) &&
                    (error.HResult & 0xffff) is 5 or 32 && File.Exists(path))
                {
                    // An open Windows reader can temporarily prevent replacing the old file.
                    // Keep it intact and retry the same flushed, private temporary file.
                    Thread.Sleep(10 << attempt);
                }
            }
        }
        finally { File.Delete(temp); }
    }

    private static FileStream Create(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var acl = new FileSecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
            return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, acl);
        }
        return new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            Share = FileShare.None, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
    }

    internal static T Read<T>(string path)
    {
        CheckPath(path);
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) &
            (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new IOException("Private file permissions must restrict access to the owner.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        try { return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidDataException("Empty private file."); }
        catch (JsonException) { throw new InvalidDataException("Invalid private file."); }
    }
}
