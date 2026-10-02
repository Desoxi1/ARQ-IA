using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace BimAiDynamoAssistant;

internal sealed record LocalAttachment(
    string Name,
    string LocalPath,
    string RelativePath,
    string Extension,
    bool IsImage,
    long SizeBytes,
    string? TextContent,
    bool TextWasTruncated);

/// <summary>
/// Keeps user-selected attachments in a private, git-ignored directory inside the
/// trusted project so the local Codex process can read references without exposing
/// the user's original file paths. The directory belongs to one panel lifetime.
/// </summary>
internal sealed class LocalAttachmentStore : IDisposable
{
    private const long MaxFileBytes = 50L * 1024 * 1024;
    private const long MaxSessionBytes = 120L * 1024 * 1024;
    private const int MaxTextChars = 20_000;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".yaml", ".yml", ".py", ".dyn",
        ".ifc", ".log", ".cs", ".js", ".ts", ".html", ".css", ".sql"
    };

    private static readonly HashSet<string> AllowedExtensions = new(ImageExtensions, StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".md", ".csv", ".json", ".xml",
        ".yaml", ".yml", ".py", ".dyn", ".ifc", ".log", ".cs", ".js", ".ts", ".html",
        ".css", ".sql", ".rvt", ".rfa", ".rte", ".dwg", ".dxf", ".nwc", ".nwd"
    };

    private readonly string _projectRoot;
    private readonly string _attachmentsRoot;
    private readonly string _sessionDirectory;
    private long _totalBytes;
    private int _fileSequence;
    private bool _disposed;

    public LocalAttachmentStore(string projectRoot)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _attachmentsRoot = Path.GetFullPath(Path.Combine(_projectRoot, "logs", "attachments"));
        _sessionDirectory = Path.Combine(_attachmentsRoot, Guid.NewGuid().ToString("N"));

        if (!Directory.Exists(_projectRoot))
            throw new DirectoryNotFoundException("No se encontró la carpeta del proyecto BIM-AI.");
        EnsureWithin(_projectRoot, _attachmentsRoot);
        EnsureWithin(_attachmentsRoot, _sessionDirectory);
        EnsureNoReparsePoint(_projectRoot);
        EnsureAttachmentDirectory();
    }

    public LocalAttachment AddFile(string sourcePath)
    {
        ThrowIfDisposed();
        var fullSource = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSource)) throw new FileNotFoundException("No se encontró el archivo adjunto.");

        var sourceAttributes = File.GetAttributes(fullSource);
        if ((sourceAttributes & FileAttributes.Directory) != 0 ||
            (sourceAttributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Selecciona un archivo normal, no una carpeta ni un enlace.");

        var extension = Path.GetExtension(fullSource);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidDataException("Ese tipo de archivo no está habilitado. Usa imágenes, documentos, archivos BIM o texto/código de referencia.");

        var sourceInfo = new FileInfo(fullSource);
        if (sourceInfo.Length <= 0) throw new InvalidDataException("El archivo está vacío.");
        if (sourceInfo.Length > MaxFileBytes) throw new InvalidDataException("Cada adjunto puede pesar hasta 50 MB.");
        if (_totalBytes + sourceInfo.Length > MaxSessionBytes)
            throw new InvalidDataException("Los adjuntos de esta conversación superan 120 MB.");

        EnsureAttachmentDirectory();
        var destination = NextDestination(extension);
        File.Copy(fullSource, destination, overwrite: false);
        if (new FileInfo(destination).Length != sourceInfo.Length)
        {
            TryDeleteFile(destination);
            throw new IOException("No se pudo verificar la copia local del adjunto.");
        }

        return Register(Path.GetFileName(fullSource), destination, extension, sourceInfo.Length);
    }

    public LocalAttachment AddClipboardImage(BitmapSource image)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);
        EnsureAttachmentDirectory();
        var destination = NextDestination(".png");
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            encoder.Save(stream);
        }
        catch
        {
            TryDeleteFile(destination);
            throw;
        }

        var length = new FileInfo(destination).Length;
        if (length <= 0 || length > MaxFileBytes || _totalBytes + length > MaxSessionBytes)
        {
            TryDeleteFile(destination);
            throw new InvalidDataException("La imagen del portapapeles supera el límite de adjuntos.");
        }

        return Register("Imagen del portapapeles.png", destination, ".png", length);
    }

    private LocalAttachment Register(string name, string destination, string extension, long length)
    {
        var fullDestination = Path.GetFullPath(destination);
        EnsureWithin(_sessionDirectory, fullDestination);
        var content = ReadTextContent(fullDestination, extension, out var truncated);
        var relativePath = Path.GetRelativePath(_projectRoot, fullDestination).Replace('\\', '/');
        _totalBytes += length;
        return new LocalAttachment(
            name,
            fullDestination,
            relativePath,
            extension,
            ImageExtensions.Contains(extension),
            length,
            content,
            truncated);
    }

    private string NextDestination(string extension)
    {
        _fileSequence++;
        var destination = Path.Combine(_sessionDirectory, $"{_fileSequence:D2}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        EnsureWithin(_sessionDirectory, destination);
        return destination;
    }

    private void EnsureAttachmentDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_projectRoot, "logs"));
        EnsureNoReparsePoint(Path.Combine(_projectRoot, "logs"));
        Directory.CreateDirectory(_attachmentsRoot);
        EnsureNoReparsePoint(_attachmentsRoot);
        Directory.CreateDirectory(_sessionDirectory);
        EnsureNoReparsePoint(_sessionDirectory);
    }

    private static string? ReadTextContent(string path, string extension, out bool truncated)
    {
        truncated = false;
        if (!TextExtensions.Contains(extension)) return null;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaxTextChars + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = reader.Read(buffer, count, buffer.Length - count);
            if (read == 0) break;
            count += read;
        }

        truncated = count > MaxTextChars;
        return new string(buffer, 0, Math.Min(count, MaxTextChars));
    }

    private static void EnsureNoReparsePoint(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("La carpeta local de adjuntos no puede ser un enlace.");
    }

    private static void EnsureWithin(string root, string candidate)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullCandidate = Path.GetFullPath(candidate);
        var relative = Path.GetRelativePath(fullRoot, fullCandidate);
        if (relative == "." || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidDataException("La ruta de adjunto sale de la carpeta temporal del proyecto.");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LocalAttachmentStore));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            EnsureWithin(_attachmentsRoot, _sessionDirectory);
            EnsureNoReparsePoint(_sessionDirectory);
            if (Directory.Exists(_sessionDirectory)) Directory.Delete(_sessionDirectory, recursive: true);
        }
        catch { }
    }
}
