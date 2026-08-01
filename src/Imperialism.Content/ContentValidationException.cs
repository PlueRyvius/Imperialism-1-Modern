namespace Imperialism.Content;

public sealed class ContentValidationException : Exception
{
    public ContentValidationException(string path, string message)
        : base($"{path}: {message}")
    {
        Path = path;
    }

    public ContentValidationException(string path, string message, Exception innerException)
        : base($"{path}: {message}", innerException)
    {
        Path = path;
    }

    public string Path { get; }
}
