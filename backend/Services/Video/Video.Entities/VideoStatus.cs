namespace Video.Entities;

/// <summary>Lifecycle of an upload: Queued → Processing → Ready (or Failed).</summary>
public enum VideoStatus
{
    /// <summary>Original file stored; waiting in the RabbitMQ queue for a worker.</summary>
    Queued = 0,

    /// <summary>A worker is transcoding it into HLS renditions.</summary>
    Processing = 1,

    /// <summary>All renditions and the thumbnail exist; visible in the feed.</summary>
    Ready = 2,

    /// <summary>Processing failed; see ProcessingError.</summary>
    Failed = 3
}
