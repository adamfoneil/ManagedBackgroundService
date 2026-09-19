namespace ManagedBackgroundServices.Abstractions.Infrastructure;

public interface ICurrentWork
{
    /// <summary>
    /// jobs may implement to indicate what they're currently doing so Dashboard can report it
    /// </summary>
    string CurrentWorkInfo { get; }
}
